// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;

namespace Extensions.Caching.Linq2Db
{
    internal class DatabaseOperations : IDatabaseOperations
    {
        

        public DatabaseOperations(
            string connectionString, string providerName, string schemaName, string tableName, ISystemClock systemClock)
        {
            ConnectionString = connectionString;
            ProviderName = providerName;
            SchemaName = schemaName;
            TableName = tableName;
            SystemClock = systemClock;
            SqlQueries = new SqlQueries(new SqlQueryTableConfiguration(){SchemaName = schemaName, TableName = tableName, SystemClock = systemClock});
        }

        protected SqlQueries SqlQueries { get; }

        protected string ConnectionString { get; }
        protected string ProviderName { get; }
        protected string SchemaName { get; }

        protected string TableName { get; }

        protected ISystemClock SystemClock { get; }

        public void DeleteCacheItem(string key)
        {
            using (var connection = _getConnection())
            {
                SqlQueries.DeleteCacheItemQueryable(connection,key).Delete();
            }
        }

        public async Task DeleteCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            using (var connection = _getConnection())
            {
                
                await SqlQueries.DeleteCacheItemQueryable(connection, key).DeleteAsync(token);
            }
        }

        public virtual byte[] GetCacheItem(string key)
        {
            return GetCacheItem(key, includeValue: true);
        }

        public virtual async Task<byte[]> GetCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            return await GetCacheItemAsync(key, includeValue: true, token: token);
        }

        public void RefreshCacheItem(string key)
        {
            GetCacheItem(key, includeValue: false);
        }

        public async Task RefreshCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
        {
            
            token.ThrowIfCancellationRequested();

            await GetCacheItemAsync(key, includeValue: false, token:token);
        }

        public virtual void DeleteExpiredCacheItems()
        {
            
            using (var connection = _getConnection())
            {
                SqlQueries.DeleteExpiredCacheItems(connection);
            }
        }

        public virtual void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var utcNow = SystemClock.UtcNow;

            var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            using (var connection = _getConnection())
            {
           
                try
                {
                    SqlQueries.SetCacheItem(connection, key, options.SlidingExpiration.HasValue ? (long)options.SlidingExpiration.Value.TotalSeconds : (long?)null, absoluteExpiration, value);
                }
                catch (DbException ex)
                {
                    if (IsDuplicateKeyException(ex))
                    {
                        // There is a possibility that multiple requests can try to add the same item to the cache, in
                        // which case we receive a 'duplicate key' exception on the primary key column.
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public virtual async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
        {
            
            token.ThrowIfCancellationRequested();

            var utcNow = SystemClock.UtcNow;

            var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            using (var connection = _getConnection())
            {

                try
                {
                    await SqlQueries.SetCacheItemAsync(connection, key, options.SlidingExpiration.HasValue ? (long)options.SlidingExpiration.Value.TotalSeconds : (long?)null, absoluteExpiration, value, token);
                }
                catch (DbException ex)
                {
                    if (IsDuplicateKeyException(ex))
                    {
                        // There is a possibility that multiple requests can try to add the same item to the cache, in
                        // which case we receive a 'duplicate key' exception on the primary key column.
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        protected virtual byte[] GetCacheItem(string key, bool includeValue)
        {
            
            using (var conn = _getConnection())
            {
                if (includeValue)
                {
                    return SqlQueries.GetCacheItem(conn,key)?.Value;
                }
                else
                {
                    SqlQueries.UpdateCacheItem(conn, key).Update();
                    return null;
                }
            }
            
        }

        protected virtual async Task<byte[]> GetCacheItemAsync(string key, bool includeValue, CancellationToken token = default(CancellationToken))
        {
            
            token.ThrowIfCancellationRequested();
            using (var conn = _getConnection())
            {
                if (includeValue)
                {
                    var set = await SqlQueries.GetCacheItemAsync(conn, key,token);
                    return set?.Value;
                }
                else
                {
                    await SqlQueries.UpdateCacheItem(conn, key).UpdateAsync(token);
                    return null;
                }
            }
        }

        private DataConnection _getConnection()
        {
            var dc = new DataConnection(ProviderName, ConnectionString);

            return dc;
        }

        /// <summary>
        /// "Tries to check if the exception denotes a duplicate key"
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        /// <remarks>
        /// Not at all proud of this heuristic. It could be way better.
        /// </remarks>
        protected bool IsDuplicateKeyException(DbException ex)
        {
            return (ex.Message.Contains("UNIQUE") || //sqlite
                    ex.Message.Contains("PRIMARY"));
        }

        protected DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
        {
            // calculate absolute expiration
            DateTimeOffset? absoluteExpiration = null;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= utcNow)
                {
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                }

                absoluteExpiration = options.AbsoluteExpiration.Value;
            }
            return absoluteExpiration;
        }

        protected void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                throw new InvalidOperationException("Either absolute or sliding expiration needs " +
                                                    "to be provided.");
            }
        }
    }
}