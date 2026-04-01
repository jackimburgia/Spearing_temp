using Microsoft.Data.SqlClient;

namespace Spearing.Utilities.Data.SqlClientUtilities
{
    public static class SqlConnectionExtensions
    {
        public static SqlConnection Connection(string connStr)
        {
            var conn = new SqlConnection(connStr);
            return conn;
        }


        public static SqlCommand Text(this SqlConnection conn, string sql)
        {
            var command = new SqlCommand(sql, conn)
            {
                CommandType = System.Data.CommandType.Text
            };

            return command;
        }

        public static SqlCommand StoredProcedure(this SqlConnection conn, string procedureName)
        {
            var command = new SqlCommand(procedureName, conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            return command;
        }
    }
}
