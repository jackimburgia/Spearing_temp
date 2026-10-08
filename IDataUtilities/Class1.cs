using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Spearing.Utilities.Data.IDataUtilities
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection(string name);
        // TODO - make extension method?
        T CreateConnection<T>(string name) where T : class, IDbConnection;
        //Task<IDbConnection> CreateConnectionAsync();

    }
}
