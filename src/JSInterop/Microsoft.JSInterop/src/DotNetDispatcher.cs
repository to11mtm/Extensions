// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.JSInterop
{
    /// <summary>
    /// Provides methods that receive incoming calls from JS to .NET.
    /// </summary>
    public static class DotNetDispatcher
    {
        internal static readonly JsonEncodedText DotNetObjectRefKey = JsonEncodedText.Encode("__dotNetObject");

        private static readonly ConcurrentDictionary<AssemblyKey, IReadOnlyDictionary<string, (MethodInfo, Type[])>> _cachedMethodsByAssembly
            = new ConcurrentDictionary<AssemblyKey, IReadOnlyDictionary<string, (MethodInfo, Type[])>>();

        /// <summary>
        /// Receives a call from JS to .NET, locating and invoking the specified method.
        /// </summary>
        /// <param name="assemblyName">The assembly containing the method to be invoked.</param>
        /// <param name="methodIdentifier">The identifier of the method to be invoked. The method must be annotated with a <see cref="JSInvokableAttribute"/> matching this identifier string.</param>
        /// <param name="dotNetObjectId">For instance method calls, identifies the target object.</param>
        /// <param name="argsJson">A JSON representation of the parameters.</param>
        /// <returns>A JSON representation of the return value, or null.</returns>
        public static string Invoke(string assemblyName, string methodIdentifier, long dotNetObjectId, string argsJson)
        {
            // This method doesn't need [JSInvokable] because the platform is responsible for having
            // some way to dispatch calls here. The logic inside here is the thing that checks whether
            // the targeted method has [JSInvokable]. It is not itself subject to that restriction,
            // because there would be nobody to police that. This method *is* the police.

            var targetInstance = (object)null;
            if (dotNetObjectId != default)
            {
                targetInstance = DotNetObjectRefManager.Current.FindDotNetObject(dotNetObjectId);
            }

            var syncResult = InvokeSynchronously(assemblyName, methodIdentifier, targetInstance, argsJson);
            if (syncResult == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(syncResult, JsonSerializerOptionsProvider.Options);
        }

        /// <summary>
        /// Receives a call from JS to .NET, locating and invoking the specified method asynchronously.
        /// </summary>
        /// <param name="callId">A value identifying the asynchronous call that should be passed back with the result, or null if no result notification is required.</param>
        /// <param name="assemblyName">The assembly containing the method to be invoked.</param>
        /// <param name="methodIdentifier">The identifier of the method to be invoked. The method must be annotated with a <see cref="JSInvokableAttribute"/> matching this identifier string.</param>
        /// <param name="dotNetObjectId">For instance method calls, identifies the target object.</param>
        /// <param name="argsJson">A JSON representation of the parameters.</param>
        /// <returns>A JSON representation of the return value, or null.</returns>
        public static void BeginInvoke(string callId, string assemblyName, string methodIdentifier, long dotNetObjectId, string argsJson)
        {
            // This method doesn't need [JSInvokable] because the platform is responsible for having
            // some way to dispatch calls here. The logic inside here is the thing that checks whether
            // the targeted method has [JSInvokable]. It is not itself subject to that restriction,
            // because there would be nobody to police that. This method *is* the police.

            // DotNetDispatcher only works with JSRuntimeBase instances.
            // If the developer wants to use a totally custom IJSRuntime, then their JS-side
            // code has to implement its own way of returning async results.
            var jsRuntimeBaseInstance = (JSRuntimeBase)JSRuntime.Current;

            // Using ExceptionDispatchInfo here throughout because we want to always preserve
            // original stack traces.
            object syncResult = null;
            ExceptionDispatchInfo syncException = null;
            object targetInstance = null;

            try
            {
                if (dotNetObjectId != default)
                {
                    targetInstance = DotNetObjectRefManager.Current.FindDotNetObject(dotNetObjectId);
                }

                syncResult = InvokeSynchronously(assemblyName, methodIdentifier, targetInstance, argsJson);
            }
            catch (Exception ex)
            {
                syncException = ExceptionDispatchInfo.Capture(ex);
            }

            // If there was no callId, the caller does not want to be notified about the result
            if (callId == null)
            {
                return;
            }
            else if (syncException != null)
            {
                // Threw synchronously, let's respond.
                jsRuntimeBaseInstance.EndInvokeDotNet(callId, false, syncException, assemblyName, methodIdentifier, dotNetObjectId);
            }
            else if (syncResult is Task task)
            {
                // Returned a task - we need to continue that task and then report an exception
                // or return the value.
                task.ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        var exception = t.Exception.GetBaseException();

                        jsRuntimeBaseInstance.EndInvokeDotNet(callId, false, ExceptionDispatchInfo.Capture(exception), assemblyName, methodIdentifier, dotNetObjectId);
                    }

                    var result = TaskGenericsUtil.GetTaskResult(task);
                    jsRuntimeBaseInstance.EndInvokeDotNet(callId, true, result, assemblyName, methodIdentifier, dotNetObjectId);
                }, TaskScheduler.Current);
            }
            else
            {
                jsRuntimeBaseInstance.EndInvokeDotNet(callId, true, syncResult, assemblyName, methodIdentifier, dotNetObjectId);
            }
        }

        private static object InvokeSynchronously(string assemblyName, string methodIdentifier, object targetInstance, string argsJson)
        {
            AssemblyKey assemblyKey;
            if (targetInstance != null)
            {
                if (assemblyName != null)
                {
                    throw new ArgumentException($"For instance method calls, '{nameof(assemblyName)}' should be null. Value received: '{assemblyName}'.");
                }

                assemblyKey = new AssemblyKey(targetInstance.GetType().Assembly);
            }
            else
            {
                assemblyKey = new AssemblyKey(assemblyName);
            }

            var (methodInfo, parameterTypes) = GetCachedMethodInfo(assemblyKey, methodIdentifier);

            var suppliedArgs = ParseArguments(methodIdentifier, argsJson, parameterTypes);

            try
            {
                return methodInfo.Invoke(targetInstance, suppliedArgs);
            }
            catch (TargetInvocationException tie) // Avoid using exception filters for AOT runtime support
            {
                if (tie.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                    throw null; // unreached
                }

                throw;
            }
        }

        internal static object[] ParseArguments(string methodIdentifier, string arguments, Type[] parameterTypes)
        {
            if (parameterTypes.Length == 0)
            {
                return Array.Empty<object>();
            }

            var utf8JsonBytes = Encoding.UTF8.GetBytes(arguments);
            var reader = new Utf8JsonReader(utf8JsonBytes);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Invalid JSON");
            }

            var suppliedArgs = new object[parameterTypes.Length];

            var index = 0;
            while (index < parameterTypes.Length && reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var parameterType = parameterTypes[index];
                if (reader.TokenType == JsonTokenType.StartObject && IsIncorrectDotNetObjectRefUse(parameterType, reader))
                {
                    throw new InvalidOperationException($"In call to '{methodIdentifier}', parameter of type '{parameterType.Name}' at index {(index + 1)} must be declared as type 'DotNetObjectRef<{parameterType.Name}>' to receive the incoming value.");
                }

                suppliedArgs[index] = JsonSerializer.Deserialize(ref reader, parameterType, JsonSerializerOptionsProvider.Options);
                index++;
            }

            if (index < parameterTypes.Length)
            {
                // If we parsed fewer parameters, we can always make a definitive claim about how many parameters were received.
                throw new ArgumentException($"The call to '{methodIdentifier}' expects '{parameterTypes.Length}' parameters, but received '{index}'.");
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            {
                // Either we received more parameters than we expected or the JSON is malformed.
                throw new JsonException($"Unexpected JSON token {reader.TokenType}. Ensure that the call to `{methodIdentifier}' is supplied with exactly '{parameterTypes.Length}' parameters.");
            }

            return suppliedArgs;

            // Note that the JsonReader instance is intentionally not passed by ref (or asn in parameter) since we want a copy of the original reader.
            static bool IsIncorrectDotNetObjectRefUse(Type parameterType, Utf8JsonReader jsonReader)
            {
                // Check for incorrect use of DotNetObjectRef<T> at the top level. We know it's
                // an incorrect use if there's a object that looks like { '__dotNetObject': <some number> },
                // but we aren't assigning to DotNetObjectRef{T}.
                if (jsonReader.Read() &&
                    jsonReader.TokenType == JsonTokenType.PropertyName &&
                    jsonReader.ValueTextEquals(DotNetObjectRefKey.EncodedUtf8Bytes))
                {
                    // The JSON payload has the shape we expect from a DotNetObjectRef instance.
                    return !parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != typeof(DotNetObjectRef<>);
                }

                return false;
            }
        }

        /// <summary>
        /// Receives notification that a call from .NET to JS has finished, marking the
        /// associated <see cref="Task"/> as completed.
        /// </summary>
        /// <remarks>
        /// All exceptions from <see cref="EndInvoke"/> are caught
        /// are delivered via JS interop to the JavaScript side when it requests confirmation, as
        /// the mechanism to call <see cref="EndInvoke"/> relies on
        /// using JS->.NET interop. This overload is meant for directly triggering completion callbacks
        /// for .NET -> JS operations without going through JS interop, so the callsite for this
        /// method is responsible for handling any possible exception generated from the arguments
        /// passed in as parameters.
        /// </remarks>
        /// <param name="arguments">The serialized arguments for the callback completion.</param>
        /// <exception cref="Exception">
        /// This method can throw any exception either from the argument received or as a result
        /// of executing any callback synchronously upon completion.
        /// </exception>
        public static void EndInvoke(string arguments)
        {
            var jsRuntimeBase = (JSRuntimeBase)JSRuntime.Current;
            ParseEndInvokeArguments(jsRuntimeBase, arguments);
        }

        internal static void ParseEndInvokeArguments(JSRuntimeBase jsRuntimeBase, string arguments)
        {
            var utf8JsonBytes = Encoding.UTF8.GetBytes(arguments);

            // The payload that we're trying to parse is of the format
            // [ taskId: long, success: boolean, value: string? | object ]
            // where value is the .NET type T originally specified on InvokeAsync<T> or the error string if success is false.
            // We parse the first two arguments and call in to JSRuntimeBase to deserialize the actual value.

            var reader = new Utf8JsonReader(utf8JsonBytes);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Invalid JSON");
            }

            reader.Read();
            var taskId = reader.GetInt64();

            reader.Read();
            var success = reader.GetBoolean();

            reader.Read();
            jsRuntimeBase.EndInvokeJS(taskId, success, ref reader);

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("Invalid JSON");
            }
        }

        /// <summary>
        /// Releases the reference to the specified .NET object. This allows the .NET runtime
        /// to garbage collect that object if there are no other references to it.
        ///
        /// To avoid leaking memory, the JavaScript side code must call this for every .NET
        /// object it obtains a reference to. The exception is if that object is used for
        /// the entire lifetime of a given user's session, in which case it is released
        /// automatically when the JavaScript runtime is disposed.
        /// </summary>
        /// <param name="dotNetObjectId">The identifier previously passed to JavaScript code.</param>
        [JSInvokable(nameof(DotNetDispatcher) + "." + nameof(ReleaseDotNetObject))]
        public static void ReleaseDotNetObject(long dotNetObjectId)
        {
            DotNetObjectRefManager.Current.ReleaseDotNetObject(dotNetObjectId);
        }

        private static (MethodInfo, Type[]) GetCachedMethodInfo(AssemblyKey assemblyKey, string methodIdentifier)
        {
            if (string.IsNullOrWhiteSpace(assemblyKey.AssemblyName))
            {
                throw new ArgumentException("Cannot be null, empty, or whitespace.", nameof(assemblyKey.AssemblyName));
            }

            if (string.IsNullOrWhiteSpace(methodIdentifier))
            {
                throw new ArgumentException("Cannot be null, empty, or whitespace.", nameof(methodIdentifier));
            }

            var assemblyMethods = _cachedMethodsByAssembly.GetOrAdd(assemblyKey, ScanAssemblyForCallableMethods);
            if (assemblyMethods.TryGetValue(methodIdentifier, out var result))
            {
                return result;
            }
            else
            {
                throw new ArgumentException($"The assembly '{assemblyKey.AssemblyName}' does not contain a public method with [{nameof(JSInvokableAttribute)}(\"{methodIdentifier}\")].");
            }
        }

        private static Dictionary<string, (MethodInfo, Type[])> ScanAssemblyForCallableMethods(AssemblyKey assemblyKey)
        {
            // TODO: Consider looking first for assembly-level attributes (i.e., if there are any,
            // only use those) to avoid scanning, especially for framework assemblies.
            var result = new Dictionary<string, (MethodInfo, Type[])>(StringComparer.Ordinal);
            var invokableMethods = GetRequiredLoadedAssembly(assemblyKey)
                .GetExportedTypes()
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly |
                    BindingFlags.Instance |
                    BindingFlags.Static))
                .Where(method => method.IsDefined(typeof(JSInvokableAttribute), inherit: false));
            foreach (var method in invokableMethods)
            {
                var identifier = method.GetCustomAttribute<JSInvokableAttribute>(false).Identifier ?? method.Name;
                var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                try
                {
                    result.Add(identifier, (method, parameterTypes));
                }
                catch (ArgumentException)
                {
                    if (result.ContainsKey(identifier))
                    {
                        throw new InvalidOperationException($"The assembly '{assemblyKey.AssemblyName}' contains more than one " +
                            $"[JSInvokable] method with identifier '{identifier}'. All [JSInvokable] methods within the same " +
                            $"assembly must have different identifiers. You can pass a custom identifier as a parameter to " +
                            $"the [JSInvokable] attribute.");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return result;
        }

        internal static bool WorkAroundTestBug;

        private static Assembly GetRequiredLoadedAssembly(AssemblyKey assemblyKey)
        {
            // We don't want to load assemblies on demand here, because we don't necessarily trust
            // "assemblyName" to be something the developer intended to load. So only pick from the
            // set of already-loaded assemblies.
            // In some edge cases this might force developers to explicitly call something on the
            // target assembly (from .NET) before they can invoke its allowed methods from JS.
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly assembly;

            if (WorkAroundTestBug)
            {
                // Workaround for https://github.com/dotnet/arcade/issues/2816
                assembly = loadedAssemblies.LastOrDefault(a => new AssemblyKey(a).Equals(assemblyKey));
            }
            else
            {
                assembly = loadedAssemblies.FirstOrDefault(a => new AssemblyKey(a).Equals(assemblyKey));

            }

            return assembly
                ?? throw new ArgumentException($"There is no loaded assembly with the name '{assemblyKey.AssemblyName}'.");
        }

        private readonly struct AssemblyKey : IEquatable<AssemblyKey>
        {
            public AssemblyKey(Assembly assembly)
            {
                Assembly = assembly;
                AssemblyName = assembly.GetName().Name;
            }

            public AssemblyKey(string assemblyName)
            {
                Assembly = null;
                AssemblyName = assemblyName;
            }

            public Assembly Assembly { get; }

            public string AssemblyName { get; }

            public bool Equals(AssemblyKey other)
            {
                if (Assembly != null && other.Assembly != null)
                {
                    return Assembly == other.Assembly;
                }

                return AssemblyName.Equals(other.AssemblyName, StringComparison.Ordinal);
            }

            public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(AssemblyName);
        }
    }
}
