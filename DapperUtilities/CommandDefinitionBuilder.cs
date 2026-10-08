using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Spearing.Utilities.Data.DapperUtilities
{


    public class CommandDefinitionBuilder
    {
        //private readonly string _connectionString;
        private readonly IDbConnection connection;

        protected string commandText;
        protected CommandType commandType = CommandType.Text;
        protected SqlParameter[] parameters;
        protected int? commandTimeout;



        // Constructor for connection string
        //public CommandDefinitionBuilder(string connectionString)
        //{
        //    this.connection = new 
        //    _connectionString = connectionString;
        //}

        // Constructor for existing connection
        public CommandDefinitionBuilder(IDbConnection connection)
        {
            this.connection = connection;
        }

        public CommandDefinitionBuilder Text(string commandText)
        {
            this.commandText = commandText;
            this.commandType = CommandType.Text;
            
            return this;
        }

        public CommandDefinitionBuilder StoredProcedure(string commandText)
        {
            this.commandText = commandText;
            this.commandType = CommandType.StoredProcedure;
            
            return this;
        }




        public CommandDefinitionBuilder Params(params SqlParameter[] parameters)
        {
            this.parameters = parameters;

            return this;
        }

        public CommandDefinitionBuilder Timeout(int? commandTimeout)
        {
            this.commandTimeout = commandTimeout;

            return this;
        }

        protected CommandDefinition GetCommandDefinition()
        {
            var dapperParams = new DynamicParameters();

            if (this.parameters != null)
            {
                foreach (var p in this.parameters)
                {
                    dapperParams.Add(p.ParameterName, p.Value);
                }
            }


            CommandDefinition commandDefinition = new CommandDefinition(
                this.commandText,
                dapperParams, 
                commandType: this.commandType, 
                commandTimeout: this.commandTimeout
                );

            return commandDefinition;
        }

        //public IEnumerable<T> Query<T>(Func<IDbConnection, CommandDefinition, IEnumerable<T>> getData)
        //{
        //    var cmd = this.GetCommandDefinition();

        //    using (var db = this.connection)
        //    {
        //        // Ensure connection is open for Dapper
        //        if (db.State != ConnectionState.Open)
        //        {
        //            db.Open();
        //        }

        //        var data = getData(db, cmd);
        //        return data;
        //    }
        //}
        public T Query<T>(Func<IDbConnection, CommandDefinition, T> getData)
        {
            var cmd = this.GetCommandDefinition();

            using (var db = connection)
            {
                // Ensure connection is open for Dapper
                if (db.State != ConnectionState.Open)
                {
                    db.Open();
                }

                var data = getData(db, cmd);
                return data;
            }
        }
        
        //public async Task<IEnumerable<T>> QueryAsync<T>(Func<IDbConnection, CommandDefinition, Task<IEnumerable<T>>> getData)
        //{
        //    var cmd = this.GetCommandDefinition();
        //    //var data = await getData(this.connection, cmd);

        //    //return data;
        //    using (var db = connection)
        //    {
        //        // Ensure connection is open for Dapper
        //        if (db.State != ConnectionState.Open)
        //        {
        //            db.Open(); // TODO - think about this; not async
        //        }

        //        var data = await getData(db, cmd);
        //        return data;
        //    }
        //}
        
        public async Task<T> QuerySingleAsync<T>(Func<IDbConnection, CommandDefinition, Task<T>> getData)
        {
            var cmd = this.GetCommandDefinition();
            //var data = await getData(this.connection, cmd);

            //return data;
            using (var db = connection)
            {
                // Ensure connection is open for Dapper
                if (db.State != ConnectionState.Open)
                {
                    db.Open(); // TODO - think about this; not async
                }

                var data = await getData(db, cmd);
                return data;
            }
        }

    }



    //public class DbReadBuilder<T>
    //{
    //    private readonly string _connectionString;
    //    private readonly SqlConnection _existingConnection;

    //    private string _sql;
    //    private DynamicParameters _parameters = new DynamicParameters();
    //    private int? _timeout;

    //    // Constructor for connection string
    //    public DbReadBuilder(string connectionString)
    //    {
    //        _connectionString = connectionString;
    //    }

    //    // Constructor for existing connection
    //    public DbReadBuilder(SqlConnection connection)
    //    {
    //        _existingConnection = connection;
    //    }

    //    public DbReadBuilder<T> WithSql(string sql)
    //    {
    //        _sql = sql;
    //        return this;
    //    }

    //    public DbReadBuilder<T> WithParameters(object parameters)
    //    {
    //        var dapperParams = new DynamicParameters(parameters);
    //        _parameters = dapperParams;
    //        return this;
    //    }

    //    // Internal helper to get the connection
    //    private SqlConnection GetConnection()
    //    {
    //        var connection = _existingConnection ?? new SqlConnection(_connectionString);
    //        return connection;
    //    }

    //    public IEnumerable<T> Query<T>(Func<IDbConnection, CommandDefinition, IEnumerable<T>> getData)
    //    {
    //        var connection = GetConnection();

    //        // Implicitly manage disposal
    //        using var db = connection;

    //        // Ensure connection is open for Dapper
    //        if (db.State != ConnectionState.Open)
    //        {
    //            db.Open();
    //        }

    //        var command = new CommandDefinition(
    //            _sql,
    //            _parameters,
    //            commandTimeout: _timeout
    //        );

    //        var data = getData(db, command);
    //        return data; 
    //    }

    //    // TERMINAL ASYNC METHOD
    //    public async Task<IEnumerable<T>> ToListAsync(CancellationToken ct = default)
    //    {
    //        var connection = GetConnection();

    //        // Implicitly manage disposal
    //        using var db = connection;

    //        // Ensure connection is open for Dapper
    //        if (db.State != ConnectionState.Open)
    //        {
    //            await db.OpenAsync(ct);
    //        }

    //        var command = new CommandDefinition(
    //            _sql,
    //            _parameters,
    //            commandTimeout: _timeout,
    //            cancellationToken: ct, commandType: CommandType.Text
    //        );

    //        //var x = db.Query<T>(command);

    //        var results = await db.QueryAsync<T>(command);
    //        return results;
    //    }
    //}
}
