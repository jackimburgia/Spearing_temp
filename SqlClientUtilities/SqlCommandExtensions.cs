using Microsoft.Data.SqlClient;
using System.Data;

namespace Spearing.Utilities.Data.SqlClientUtilities
{
    public static class SqlCommandExtensions
    {
        public static SqlCommand Params(this SqlCommand cmd, params SqlParameter[] parameters)
        {
            if (parameters != null)
            {
                cmd.Parameters.AddRange(parameters);
            }
            return cmd;
        }

        public static SqlCommand Timeout(this SqlCommand cmd, int seconds)
        {
            cmd.CommandTimeout = seconds;
            return cmd;
        }

        #region Single
        public static T? QuerySingle<T>(this SqlCommand cmd, Func<IDataReader, T> getItem)
        {
            using (var conn = cmd.Connection)
            {
                using (cmd)
                {
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var item = getItem(reader);
                            return item;
                        }
                    }
                }
            }

            return default;
        }


        // TODO - QuerySingleAsync

        #endregion

        // TODO - make connection / command management consistent

        #region IEnumerable
        // 1. Synchronous (Buffered via yield)
        public static IEnumerable<T> Query<T>(this SqlCommand cmd, Func<IDataReader, T> getItem)
        {
            using (var conn = cmd.Connection)
            {
                if (conn.State != ConnectionState.Open)
                    conn.Open();

                using(var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = getItem(reader);
                        yield return item;
                    }
                }
            }
        }

        // 2. Asynchronous (Buffered - Returns full set when Task completes)
        public static async Task<IEnumerable<T>> QueryAsync<T>(this SqlCommand cmd, Func<IDataRecord, T> getItem)
        {
            var results = new List<T>();
            using (var conn = cmd.Connection)
            using (cmd)
            {
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var item = getItem(reader);

                    results.Add(item);
                }
            }
            return results; // List<T> implicitly casts to IEnumerable<T>
        }

        // 3. Asynchronous (Streaming - Yields items as they arrive)
        public static async IAsyncEnumerable<T> QueryStreamAsync<T>(this SqlCommand cmd, Func<IDataRecord, T> getItem)
        {
            using (var conn = cmd.Connection)
            using (cmd)
            {
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var item = getItem(reader);
                        yield return item;
                    }
                }
            }
        }
        #endregion


        #region Non-Query
        // Synchronous Insert/Update/Delete
        public static int ExecNonQuery(this SqlCommand cmd)
        {
            using (var conn = cmd.Connection)
            using (cmd)
            {
                if (conn.State != ConnectionState.Open) 
                    conn.Open();

                var result = cmd.ExecuteNonQuery();
                return result;
            }
        }

        // Asynchronous Insert/Update/Delete (.NET 10 Standard)
        public static async Task<int> ExecNonQueryAsync(this SqlCommand cmd, CancellationToken ct = default)
        {
            using (var conn = cmd.Connection)
            using (cmd)
            {
                if (conn.State != ConnectionState.Open) 
                    await conn.OpenAsync(ct);

                var result = await cmd.ExecuteNonQueryAsync(ct);
                return result;
            }
        }
        #endregion


        #region Scalar
        public static T? ExecScalar<T>(this SqlCommand cmd)
        {
            using (var conn = cmd.Connection)
            using (cmd)
            {
                if (conn.State != ConnectionState.Open) conn.Open();
                object result = cmd.ExecuteScalar();
                return result == DBNull.Value ? default : (T)result;
            }
        }

        public static async Task<T?> ExecScalarAsync<T>(this SqlCommand cmd, CancellationToken ct = default)
        {
            using (var conn = cmd.Connection)
            using (cmd)
            {
                if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
                object result = await cmd.ExecuteScalarAsync(ct);
                return result == DBNull.Value ? default : (T)result;
            }
        }
        #endregion


    }
}
