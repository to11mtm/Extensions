// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Linq;
using Microsoft.Extensions.Internal;

namespace Extensions.Caching.Linq2Db
{
    public static class SqlQueryHelper
    {
        
    }

    internal class SqlQueries
    {
        private const string TableInfoFormat =
            "SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE " +
            "FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_SCHEMA = '{0}' " +
            "AND TABLE_NAME = '{1}'";

        private const string UpdateCacheItemFormat =
        "UPDATE {0} " +
        "SET ExpiresAtTime = " +
            "(CASE " +
                "WHEN DATEDIFF(SECOND, @UtcNow, AbsoluteExpiration) <= SlidingExpirationInSeconds " +
                "THEN AbsoluteExpiration " +
                "ELSE " +
                "DATEADD(SECOND, SlidingExpirationInSeconds, @UtcNow) " +
            "END) " +
        "WHERE Id = @Id " +
        "AND @UtcNow <= ExpiresAtTime " +
        "AND SlidingExpirationInSeconds IS NOT NULL " +
        "AND (AbsoluteExpiration IS NULL OR AbsoluteExpiration <> ExpiresAtTime) ;";

        public IUpdatable<CacheTable> UpdateCacheItem(DataConnection conn, string id)
        {
            var utcNow = SystemClock.UtcNow.UtcDateTime;
            return conn.GetTable<CacheTable>().SchemaName(SchemaName).TableName(TableName).Where(cacheTable =>
                    cacheTable.Id == id && cacheTable.SlidingExpirationInSeconds != null &&
                    utcNow <= cacheTable.ExpiresAtTime &&
                    (cacheTable.AbsoluteExpiration == null ||
                     cacheTable.AbsoluteExpiration != cacheTable.ExpiresAtTime))
                .Set(r => r.ExpiresAtTime,
                    r => r.AbsoluteExpiration != null && 
                        Sql.DateAdd(Sql.DateParts.Second,r.SlidingExpirationInSeconds, r.AbsoluteExpiration) <= utcNow
                         ? r.AbsoluteExpiration
                        : Sql.DateAdd(Sql.DateParts.Second, r.SlidingExpirationInSeconds, utcNow));
        }

        public static string SchemaName { get; set; }

        public static string TableName { get; set; }
        public ISystemClock SystemClock { get; }
        
                                 

        public IQueryable<CacheTable> GetCacheItemGet(DataConnection conn, string id)
        {
            var utcNow = SystemClock.UtcNow.UtcDateTime;
            return conn.GetTable<CacheTable>().SchemaName(SchemaName).TableName(TableName)
                .Where(ct => ct.Id == id && utcNow <= ct.ExpiresAtTime).Select(ct => new CacheTable()
                {
                    AbsoluteExpiration = Sql.Convert(Sql.DateTime2, ct.AbsoluteExpiration),
                    ExpiresAtTime = Sql.Convert(Sql.DateTime2, ct.ExpiresAtTime), Value = ct.Value,
                    SlidingExpirationInSeconds = ct.SlidingExpirationInSeconds, Id = ct.Id
                });
        }

        public async Task<CacheTable> GetCacheItemAsync(DataConnection conn, string id, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            await UpdateCacheItem(conn, id).UpdateAsync(token);
            return await GetCacheItemGet(conn, id).FirstOrDefaultAsync(token);
        }

        public CacheTable GetCacheItem(DataConnection conn, string id)
        {
            UpdateCacheItem(conn, id).Update();
            return GetCacheItemGet(conn, id).FirstOrDefault();
        }

        

        public async Task SetCacheItemAsync(DataConnection conn, string id, long? slidingExpirationInSeconds, DateTimeOffset? absoluteExpiration, byte[] valueBytes, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            var absoluteExpirUtcDt = absoluteExpiration?.UtcDateTime;
            var expiresAtTime = slidingExpirationInSeconds.HasValue
                ? SystemClock.UtcNow.AddSeconds(slidingExpirationInSeconds.Value).UtcDateTime
                : absoluteExpiration.GetValueOrDefault(SystemClock.UtcNow).UtcDateTime;
            await conn.GetTable<CacheTable>().SchemaName(SchemaName).TableName(TableName)
                .InsertOrUpdateAsync(() => new CacheTable()
                    {
                        Id = id,
                        Value = valueBytes,
                        ExpiresAtTime = expiresAtTime,
                        SlidingExpirationInSeconds = slidingExpirationInSeconds,
                        AbsoluteExpiration = absoluteExpirUtcDt
                    },
                    (ct) =>

                        new CacheTable()
                        {
                            Value = valueBytes,
                            ExpiresAtTime = expiresAtTime,
                            SlidingExpirationInSeconds = slidingExpirationInSeconds,
                            AbsoluteExpiration = absoluteExpirUtcDt
                        },
                    () =>
                        new CacheTable()
                        {
                            Id = id
                        }, 
                    token
                );
        }

        public void SetCacheItem(DataConnection conn, string id, long? slidingExpirationInSeconds, DateTimeOffset? absoluteExpiration, byte[] valueBytes)
        {
            var absoluteExpirUtcDt = absoluteExpiration?.UtcDateTime;
            var expiresAtTime = slidingExpirationInSeconds.HasValue
                ? SystemClock.UtcNow.AddSeconds(slidingExpirationInSeconds.Value).UtcDateTime
                : absoluteExpiration.GetValueOrDefault(SystemClock.UtcNow).UtcDateTime;
            conn.GetTable<CacheTable>().SchemaName(SchemaName).TableName(TableName)
                .InsertOrUpdate(() => new CacheTable()
                    {
                        Id = id,
                        Value = valueBytes,
                        ExpiresAtTime = expiresAtTime,
                        SlidingExpirationInSeconds = slidingExpirationInSeconds,
                        AbsoluteExpiration = absoluteExpirUtcDt
                    },
                    (ct) =>

                        new CacheTable()
                        {
                            Value = valueBytes,
                            ExpiresAtTime = expiresAtTime,
                            SlidingExpirationInSeconds = slidingExpirationInSeconds,
                            AbsoluteExpiration = absoluteExpirUtcDt
                        },
                    () =>
                        new CacheTable()
                        {
                            Id = id
                        }
                );
        }

        private const string SetCacheItemFormat =
            "DECLARE @ExpiresAtTime DATETIMEOFFSET; " +
            "SET @ExpiresAtTime = " +
            "(CASE " +
                    "WHEN (@SlidingExpirationInSeconds IS NUll) " +
                    "THEN @AbsoluteExpiration " +
                    "ELSE " +
                    "DATEADD(SECOND, Convert(bigint, @SlidingExpirationInSeconds), @UtcNow) " +
            "END);" +
            "UPDATE {0} SET Value = @Value, ExpiresAtTime = @ExpiresAtTime," +
            "SlidingExpirationInSeconds = @SlidingExpirationInSeconds, AbsoluteExpiration = @AbsoluteExpiration " +
            "WHERE Id = @Id " +
            "IF (@@ROWCOUNT = 0) " +
            "BEGIN " +
                "INSERT INTO {0} " +
                "(Id, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration) " +
                "VALUES (@Id, @Value, @ExpiresAtTime, @SlidingExpirationInSeconds, @AbsoluteExpiration); " +
            "END ";

        public IQueryable<CacheTable> DeleteCacheItemQueryable(DataConnection conn, string id)
        {
            return conn.GetTable<CacheTable>().SchemaName(SchemaName).TableName(TableName)
                .Where(r => r.Id == id);
        }

        public void DeleteExpiredCacheItems(DataConnection conn)
        {
            var utcNow = SystemClock.UtcNow.UtcDateTime;
            conn.GetTable<CacheTable>().SchemaName(SchemaName).TableName(TableName)
                .Where(r =>  utcNow > r.ExpiresAtTime)
                .Delete();
        }

        private const string DeleteCacheItemFormat = "DELETE FROM {0} WHERE Id = @Id";

        public const string DeleteExpiredCacheItemsFormat = "DELETE FROM {0} WHERE @UtcNow > ExpiresAtTime";

        public SqlQueries(SqlQueryTableConfiguration config)
        {
            SchemaName = config.SchemaName;
            TableName = config.TableName;
            SystemClock = config.SystemClock;
        }

        
    }

    internal class SqlQueryTableConfiguration
    {
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public ISystemClock SystemClock { get; set; }
    }
}
