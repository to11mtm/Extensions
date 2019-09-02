using System.Data.Common;
using System.Data.SQLite;
using LinqToDB;
using Xunit.Abstractions;

namespace Extensions.Caching.Linq2Db.Tests
{
    public class SystemDataSQLiteCacheWithDatabaseTest : Linq2DbCacheBaseTest
    {

        protected static string connString = "FullUri=file:memdb1?mode=memory&cache=shared";
        private static readonly SQLiteConnection heldFakeDb = new SQLiteConnection(connString).OpenAndReturn();
        public SystemDataSQLiteCacheWithDatabaseTest(ITestOutputHelper outputHelper) : base(outputHelper,connString,ProviderName.SQLiteClassic)
        {
            
        }


        public override DbConnection GetConnection()
        {
            return  new SQLiteConnection(connString);
        }


    }
}