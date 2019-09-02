# Extensions.Caching.Linq2Db : Database agnostic IDistributedCache Implementation

This is an implementation of IDistributedCache utilizing Linq2Db. It is based on a fork of the Microsoft provided SQL Server Implementation and should be compatible at the table level.

## "I don't want to use MSSQL" aka "Why does this library exist?"

Linq2Db supports many databases including:

 - MSSQL
 - SQLite 
 - Oracle
 - MySql
 - Postgres 
 - Firebird

 The Unit tests provided show compatibility for many of these.

You may use any table that is compatible with the structure of the CacheTable object below, (comments included)

```
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
```

#USAGE:

```
//In this example, we are using the ServiceCollection abstraction as one may tend to in the ASPNET world...

services.AddDistributedLinq2DbCache((LinqToDbCacheOptions options) => {
                options.ConnectionString = "MyDatabaseConnectionString";
                options.SchemaName = "MySchema";
                options.TableName = "MyTable";
                options.ProviderName = LinqToDB.ProviderName.SqlServer2008; //Lots of options here
				                                                            //And not just MSSQL.

				options.ExpiredItemsDeletionInterval == null //Optional TimeSpan. 
				                                             //Must be no less than 5 Minutes. 
				                                             //Default is 30 minutes.

			    options.DefaultSlidingExpiration == null //Optional Timespan.
				                                         //Must be Greater than Zero.
				                                         //Default is 20 minutes.

            });
```

If you don't want to use the `ServiceCollection` abstraction, you can just use `LinqToDbCache` as your `IDistributedCache` implementation. It takes the same `LinqToDbCacheOptions` type as documented above.

