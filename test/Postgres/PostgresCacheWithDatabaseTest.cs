using System;
using System.Data.Common;
using LinqToDB;
using Npgsql;
using Xunit.Abstractions;

namespace Extensions.Caching.Linq2Db.Tests
{
    public class PostgresCacheWithDatabaseTest : Linq2DbCacheBaseTest
    {
        protected static string connString =
            @"Server=192.168.99.100;Port=32774;Database=testcache;User Id=testcache;Password=testcache;";
        public PostgresCacheWithDatabaseTest(ITestOutputHelper outputHelper) : base(outputHelper, connString, ProviderName.PostgreSQL95)
        {
            try
            {
                using (var conn = new NpgsqlConnection(connString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "Create schema if not exists dbo";
                        cmd.ExecuteNonQuery();
                    }

                }
            }
            catch (Exception e)
            {
                outputHelper.WriteLine(e.ToString());
            }
        }

        public override DbConnection GetConnection()
        {
            return new NpgsqlConnection(connString);
        }
    }
}