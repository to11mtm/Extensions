// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.JSInterop
{
    public static partial class DotNetDispatcher
    {
        public static void BeginInvoke(string callId, string assemblyName, string methodIdentifier, long dotNetObjectId, string argsJson) { }
        public static void EndInvoke(string arguments) { }
        public static string Invoke(string assemblyName, string methodIdentifier, long dotNetObjectId, string argsJson) { throw null; }
        [Microsoft.JSInterop.JSInvokableAttribute("DotNetDispatcher.ReleaseDotNetObject")]
        public static void ReleaseDotNetObject(long dotNetObjectId) { }
    }
    public static partial class DotNetObjectRef
    {
        public static Microsoft.JSInterop.DotNetObjectRef<TValue> Create<TValue>(TValue value) where TValue : class { throw null; }
    }
    public sealed partial class DotNetObjectRef<TValue> : System.IDisposable where TValue : class
    {
        internal DotNetObjectRef() { }
        public TValue Value { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
        public void Dispose() { }
    }
    public partial interface IJSInProcessRuntime : Microsoft.JSInterop.IJSRuntime
    {
        T Invoke<T>(string identifier, params object[] args);
    }
    public partial interface IJSRuntime
    {
        System.Threading.Tasks.Task<TValue> InvokeAsync<TValue>(string identifier, System.Collections.Generic.IEnumerable<object> args, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));
        System.Threading.Tasks.Task<TValue> InvokeAsync<TValue>(string identifier, params object[] args);
    }
    public partial class JSException : System.Exception
    {
        public JSException(string message) { }
        public JSException(string message, System.Exception innerException) { }
    }
    public abstract partial class JSInProcessRuntimeBase : Microsoft.JSInterop.JSRuntimeBase, Microsoft.JSInterop.IJSInProcessRuntime, Microsoft.JSInterop.IJSRuntime
    {
        protected JSInProcessRuntimeBase() { }
        protected abstract string InvokeJS(string identifier, string argsJson);
        public TValue Invoke<TValue>(string identifier, params object[] args) { throw null; }
    }
    [System.AttributeUsageAttribute(System.AttributeTargets.Method, AllowMultiple=true)]
    public partial class JSInvokableAttribute : System.Attribute
    {
        public JSInvokableAttribute() { }
        public JSInvokableAttribute(string identifier) { }
        public string Identifier { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } }
    }
    public static partial class JSRuntime
    {
        public static void SetCurrentJSRuntime(Microsoft.JSInterop.IJSRuntime instance) { }
    }
    public abstract partial class JSRuntimeBase : Microsoft.JSInterop.IJSRuntime
    {
        protected JSRuntimeBase() { }
        protected System.TimeSpan? DefaultAsyncTimeout { [System.Runtime.CompilerServices.CompilerGeneratedAttribute]get { throw null; } [System.Runtime.CompilerServices.CompilerGeneratedAttribute]set { } }
        protected abstract void BeginInvokeJS(long taskId, string identifier, string argsJson);
        protected internal abstract void EndInvokeDotNet(string callId, bool success, object resultOrError, string assemblyName, string methodIdentifier, long dotNetObjectId);
        public System.Threading.Tasks.Task<T> InvokeAsync<T>(string identifier, System.Collections.Generic.IEnumerable<object> args, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken)) { throw null; }
        public System.Threading.Tasks.Task<T> InvokeAsync<T>(string identifier, params object[] args) { throw null; }
    }
}
