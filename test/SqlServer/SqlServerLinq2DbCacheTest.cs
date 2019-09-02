using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Data;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Extensions.Caching.Linq2Db.Tests
{
    public class SqlServerLinq2DbCacheTest : Linq2DbCacheBaseTest
    {
        public static string connString = "Server=(localdb)\\CacheTestDb;Database=master;Trusted_Connection=True;";

        public SqlServerLinq2DbCacheTest(ITestOutputHelper outputHelper) : base(outputHelper, connString,
            LinqToDB.ProviderName.SqlServer)
        {

        }

        public override DbConnection GetConnection()
        {
            return new SqlConnection(connString);
        }
    }
}
