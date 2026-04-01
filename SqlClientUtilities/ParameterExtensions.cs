using Microsoft.Data.SqlClient;

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
