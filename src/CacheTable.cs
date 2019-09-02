// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using LinqToDB;
using LinqToDB.Mapping;

namespace Extensions.Caching.Linq2Db
{
    /// <summary>
    /// Cache table Structure used by Linq2Db
    /// </summary>
    public class CacheTable
    {
        /// <summary>
        /// Primary Key
        /// </summary>
        [Column(
            IsPrimaryKey = true, 
            DataType = DataType.NVarChar, //Since NET strings are unicode we do not do ANSI.
            Scale = 449,  //This is based on SQL Server Rules
            Length = 449, //for max size of a primary key.
            CanBeNull = false
            )]
        public string Id { get; set; }
        
        /// <summary>
        /// Serialized Value.
        /// </summary>
        [Column(CanBeNull = false)]
        public byte[] Value { get; set; }

        /// <summary>
        /// Expiration time.
        /// </summary>
        /// <remarks>
        /// We use DateTime and always treat as UTC (using the UtcDateTime property) when building the query.
        /// This is done because some DB Engines just don't have a concept of DateTimeOffset that is usable.
        /// SQLite is probably the biggest example, but I believe MYSQL and Firebird also cannot.
        ///
        /// If your DB of choice requires hinting for a datatype, PRs are welcome!
        /// </remarks>
        [Column(Configuration = ProviderName.SqlServer, DataType = DataType.DateTimeOffset)]
        public DateTime ExpiresAtTime { get; set; }

        /// <summary>
        /// Says it all.
        /// </summary>
        /// <remarks>
        /// This type was picked to be compliant with the MSSQL Implementation,
        /// which stores this column as BIGINT.
        /// </remarks>
        public long? SlidingExpirationInSeconds { get; set; }

        /// <summary>
        /// Absolute Expiration Time
        /// </summary>
        /// <remarks>
        /// All Comments about ExpiresAtTime above apply here.
        /// </remarks>
        [Column(Configuration = ProviderName.SqlServer, DataType = DataType.DateTimeOffset)]
        public DateTime? AbsoluteExpiration { get; set; }
    }
    
}
