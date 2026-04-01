using System.Data;

namespace Spearing.Utilities.Data.DapperUtilities
{
    public static class ConnectionExtensions
    {
        public static CommandDefinitionBuilder CommandDefinition(this IDbConnection connection)
        {
            var builder = new CommandDefinitionBuilder(connection);

            return builder;
        }
    }
}
