# Extensions.Caching.Linq2Db Tests

These unit tests are based on (forked from) the tests provided in the Microsoft.Extensions.Caching.SqlServer package.

If you don't need a specific provider, you can of course easily Exclued from the build.

However, The documentation for these tests for now resides here. It will be properly cleaned up and partitioned out later.

## Pre-requisites


1. A functional DB, See notes for specific providers default setup.

 - Firebird
   - This was tested against a firebird Docker container
     - 'testcache' was just used for every variable possible, as seen by connection string.
   - Wirecrypt was disabled for ease of testing functionality
     - EnableLegacyClientAuth was set to true on the container to facilitate this. 
 - Postgres
   - This was tested against the postgres 9.6 docker container.
 - SQL Server:
   - Local DB included with VS is sufficient
   - An empty database named `CacheTestDb` in that SQL Server


The Test Helper itself should handle the rest of the table creation as long as the account has CREATE TABLE permission.
You may also use the `dotnet-sql-cache` table structure if you so choose.


## Per the original notes:

## Running the tests

1. Install the latest version of the `dotnet-sql-cache` too: `dotnet tool install --global dotnet-sql-cache` (make sure to specify a version if it's a pre-release tool you want!)
1. Run `dotnet sql-cache [connectionstring] dbo CacheTest`
    * `[connectionstring]` must be a SQL Connection String **for an empty database named `CacheTestDb` that already exists**
    * If using Local DB, this string should work: `"Server=(localdb)\MSSQLLocalDB;Database=CacheTestDb;Trusted_Connection=True;"`

 > These tests include functional tests that run against a real SQL Server. 
Since these are flaky on CI, they should be run manually when changing this code.

I do not know if this is the case as I have not tested this implementation on a CI.