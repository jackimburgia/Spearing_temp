using Microsoft.Data.SqlClient;
using static Spearing.Utilities.Data.SqlClientUtilities.SqlConnectionExtensions;
using static Spearing.Utilities.Data.SqlClientUtilities.ParameterUtilities;
using Spearing.Utilities.Data.SqlClientUtilities;
using Microsoft.Data.SqlClient; // or System.Data.SqlClient
using Dapper;
using Spearing.Utilities.Data.DapperUtilities;

namespace DataTester
{
    public class Asset
    {
        public string AssetID { get; set; }
        public int Days { get; set; }
    }

    public class Order
    {
        public int order_id { get; set; }
        public string customer_id { get; set; }
        public DateTime order_date { get; set; }
    }

    internal class Program
    {
        static async Task MainAsync()
        {
            // Dapper extensions
            var connStr = "Server=myServer;Database=myDB;User Id=myUser;Password=myPassword;";
            var conn = new SqlConnection(connStr);




            var res2 = await new CommandDefinitionBuilder(conn)
                .QueryAsync((db, cmd) => db.QueryAsync<Asset>(cmd));
        }

        static void Main(string[] args)
        {
            string connStr = @"Server=localhost\SQLEXPRESS;Database=tester;Trusted_Connection=True;TrustServerCertificate=True;";

            // SQLClient extensions
            var resultsAdo = Connection(connStr)
            .Text("SELECT * FROM Orders WHERE customer_id = @customer_id")
                .Params(
                    CharParam("@customer_id").Value("QUEDE")
                )
                .Query(dr => new Order()
                {
                    order_id = Convert.ToInt32(dr["order_id"]),
                    customer_id = Convert.ToString(dr["customer_id"]),
                    order_date = Convert.ToDateTime(dr["order_date"]),
                })
                .ToArray();
            
            var singleAdo = Connection(connStr)
            .Text("SELECT * FROM Orders WHERE order_id = @order_id")
                .Params(
                    IntParam("@order_id").Value(10261)
                )
                .QuerySingle(dr => new Order()
                {
                    order_id = Convert.ToInt32(dr["order_id"]),
                    customer_id = Convert.ToString(dr["customer_id"]),
                    order_date = Convert.ToDateTime(dr["order_date"]),
                });


            // dapper
            var resultsDapper = Connection(connStr)
                .CommandDefinition()
                .Text("SELECT * FROM Orders WHERE customer_id = @customer_id")
                .Params(
                    CharParam("@customer_id").Value("QUEDE")
                )
                .Query((db, cmd) => db.Query<Order>(cmd));

            
            var singleDapper = Connection(connStr)
                .CommandDefinition()
                .Text("SELECT * FROM Orders WHERE order_id = @order_id")
                .Params(
                    IntParam("@order_id").Value(10261)
                )
                .Query((db, cmd) => db.QuerySingleOrDefault<Order>(cmd));



            return;




            var conn = Connection("conn str"); 
            //var res = conn.Query<>()
            //var res = conn.QueryFirst<>
            //var res = conn.QueryFirstOrDefault<>
            //var res = conn.QuerySingle<>
            //var res = conn.QuerySingleOrDefault<>

            var res2 = Connection("conn str")
                .CommandDefinition()
                .Query((db, cmd) => db.Query<Asset>(cmd));


            var single1 = Connection("conn str")
                .CommandDefinition()
                .Query((db, cmd) => db.QuerySingleOrDefault<Asset>(cmd));











            //using (SqlConnection connection = new SqlConnection("connectionString"))
            //{
            //    connection.Open();
            //    using (SqlCommand command = connection.CreateCommand())
            //    {

            //    }
            //}
        }
    }
}
