// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Extensions.Caching.Linq2Db
{
    /// <summary>
    /// Extension methods for setting up Microsoft SQL Server distributed cache services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class SqlServerCachingServicesExtensions
    {
        /// <summary>
        /// Adds Microsoft SQL Server distributed caching services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="setupAction">An <see cref="Action{LinqToDbCacheOptions}"/> to configure the provided <see cref="LinqToDbCacheOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddDistributedLinq2DbCache(this IServiceCollection services, Action<LinqToDbCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            AddSqlServerCacheServices(services);
            services.Configure(setupAction);

            return services;
        }

        // to enable unit testing
        public static void AddSqlServerCacheServices(IServiceCollection services)
        {
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, LinqToDbCache>());
        }
    }
}