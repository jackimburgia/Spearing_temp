using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spearing.Data.ComplianceData;
using Spearing.Data.IComplianceData;
using Spearing.Utilities.Data.DapperUtilities;
using Spearing.Utilities.Data.IDataUtilities;
using Spearing.Utilities.Data.SqlClientUtilities;
using static Spearing.Utilities.Data.SqlClientUtilities.ParameterUtilities;
using static Spearing.Utilities.Data.SqlClientUtilities.SqlConnectionExtensions;

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
                .Query((db, cmd) => db.QueryAsync<Asset>(cmd));
        }

        protected IModelDefinitionData modelDefinitionData;

        public Program(IConfiguration configuration, IModelDefinitionData modelDefinitionData)
        {
            this.modelDefinitionData = modelDefinitionData;

            var connStr = configuration.GetConnectionString("Compliance");
            Console.WriteLine($"Connection: {connStr}");
        }

        public void Run()
        {
            var data = this.modelDefinitionData.GetModelDefinition(1);  
            this.modelDefinitionData.UpdateModelDefinition(data);
        }

        static void Main(string[] args)
        {


            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var serviceProvider = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddTransient<Program>()
                .AddTransient<IDbConnectionFactory, SqlServerConnectionFactory>()
                .AddTransient<IModelDefinitionData, ModelDefinitionData>()
                //.AddDependencies()
                .BuildServiceProvider();

            var program = serviceProvider.GetService<Program>();

            program.Run();


            return;

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
