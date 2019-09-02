using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Extensions.Caching.Linq2Db.Tests
{
    public class FirebirdCacheWithDatabaseTest : Linq2DbCacheBaseTest
    {
        protected static string connString =
            @"User=SYSDBA;Password=testcache;Database=/firebird/data/testcache;DataSource=192.168.99.100;Port=32778;Dialect=3;Charset=NONE;Role=;Connection lifetime=15;Pooling=true;MinPoolSize=0;MaxPoolSize=50;Packet Size=8192;ServerType=0;WireCrypt=Disabled;";

        public FirebirdCacheWithDatabaseTest(ITestOutputHelper outputHelper) : base(outputHelper, connString,
            ProviderName.Firebird)
        {

        }

        public override DbConnection GetConnection()
        {
            return new FirebirdSql.Data.FirebirdClient.FbConnection(connString);
        }
    }
}
