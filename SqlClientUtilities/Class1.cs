using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Spearing.Utilities.Data.IDataUtilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Spearing.Utilities.Data.SqlClientUtilities
{
    public class SqlServerConnectionFactory : IDbConnectionFactory
    {
        protected IConfiguration configuration;

        public SqlServerConnectionFactory(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public IDbConnection CreateConnection(string name)
        {
            string connectionString = this.configuration.GetConnectionString(name);
            var conn = new SqlConnection(connectionString);
            return conn;
        }

        public T CreateConnection<T>(string name) where T : class, IDbConnection
        {
            var conn = CreateConnection(name) as T;

            return conn;
        }
    }
}
