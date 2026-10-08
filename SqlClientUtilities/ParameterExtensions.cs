using Microsoft.Data.SqlClient;
using Spearing.Utilities.Entities.EntitiesUtilities;
using static Spearing.Utilities.Data.SqlClientUtilities.ParameterUtilities;


namespace Spearing.Utilities.Data.SqlClientUtilities
{
    public static class ParameterExtensions
    {
        public static SqlParameter Value<T>(this SqlParameter param, T? value)
        {
            if (value is null)
            {
                param.Value = DBNull.Value;
            }
            // Check for string specifically to handle your "Empty as Null" logic
            else if (value is string s && string.IsNullOrEmpty(s))
            {
                param.Value = DBNull.Value;
            }
            else
            {
                param.Value = value;
            }

            return param;
        }

        public static bool ContainsParameter(this SqlCommand command, string name)
        {
            var parameterIndex = command.Parameters.IndexOf(name);

            // Check if it's found
            var exists = parameterIndex >= 0;

            return exists;
        }

        public static SqlCommand SetParameter(this SqlCommand command, SqlParameter parameter)
        {
            if (command.ContainsParameter(parameter.ParameterName))
            {
                command.Parameters[parameter.ParameterName] = parameter;
            }
            else
            {
                command.Parameters.Add(parameter);
            }

            return command;
        }


        public static SqlCommand Modifiable(this SqlCommand command, IModifiable modifiable)
        {
            command
                .SetParameter(VarCharParam(nameof(IModifiable.CreatedBy)).Value(modifiable.CreatedBy))
                .SetParameter(DateTimeParam(nameof(IModifiable.CreatedDate)).Value(modifiable.CreatedDate))
                .SetParameter(VarCharParam(nameof(IModifiable.ModifiedBy)).Value(modifiable.ModifiedBy))
                .SetParameter(DateTimeParam(nameof(IModifiable.ModifiedDate)).Value(modifiable.ModifiedDate));

            return command;
        }

        //public static SqlParameter Value(this SqlParameter param, object value)
        //{
        //    if (value == null)
        //    {
        //        param.Value = DBNull.Value;
        //    }
        //    else if (String.IsNullOrEmpty(value.ToString()))
        //    {
        //        param.Value = DBNull.Value;
        //    }
        //    else
        //    {
        //        param.Value = value;
        //    }

        //    return param;
        //}
    }
}
