// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.



using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Extensions.Caching.Linq2Db
{
    /// <summary>
    /// SynchronizationContextManager controls SynchronizationContext of the async pipeline.
    /// <para>Does the same thing as .ConfigureAwait(false) but better - it should be written once only,
    /// unlike .ConfigureAwait(false).</para>
    /// <para>  
    /// Should be used as a very first line inside async public API of the library code</para>
    /// </summary>
    /// <example> 
    /// This sample shows how to use <see cref="SynchronizationContextManager"/> .
    /// <code>
    /// class CoolLib 
    /// {
    ///     public async Task DoSomething() 
    ///     {
    ///         
    ///         
    ///         await DoSomethingElse();
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// This is unashamedly borrowed from Akka.NET since the licensing is compatible.
    /// </remarks>
    internal static class SynchronizationContextManager
    {
        public static ContextRemover RemoveContext { get; } = new ContextRemover();
    }
    
    internal class ContextRemover : INotifyCompletion
    {
        public bool IsCompleted => SynchronizationContext.Current == null;

        public void OnCompleted(Action continuation)
        {
            var prevContext = SynchronizationContext.Current;

            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                continuation();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        public ContextRemover GetAwaiter()
        {
            return this;
        }

        public void GetResult()
        {
            // empty on purpose
        }
    }
}
