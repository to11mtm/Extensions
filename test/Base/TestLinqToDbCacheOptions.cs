// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Options;

namespace Extensions.Caching.Linq2Db.Tests
{
    internal class TestLinqToDbCacheOptions : IOptions<LinqToDbCacheOptions>
    {
        private readonly LinqToDbCacheOptions _innerOptions;

        public TestLinqToDbCacheOptions(LinqToDbCacheOptions innerOptions)
        {
            _innerOptions = innerOptions;
        }

        public LinqToDbCacheOptions Value
        {
            get
            {
                return _innerOptions;
            }
        }
    }
}
