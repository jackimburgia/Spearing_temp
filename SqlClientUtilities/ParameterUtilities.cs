using Microsoft.Data.SqlClient;
using Spearing.Utilities.Entities.EntitiesUtilities;
//using Spearing.Utilities.Security.SecurityUtilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Utilities.Data.SqlClientUtilities
{



    public static class ParameterUtilities
    {


        // Numeric
        public static SqlParameter IntParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Int);
            return param;
        }

        public static SqlParameter BigIntParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.BigInt);
            return param;
        }

        public static SqlParameter SmallIntParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.SmallInt);
            return param;
        }

        public static SqlParameter TinyIntParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.TinyInt);
            return param;
        }

        public static SqlParameter DecimalParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Decimal);
            return param;
        }

        public static SqlParameter MoneyParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Money);
            return param;
        }

        public static SqlParameter FloatParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Float);
            return param;
        }


        // String and text
        public static SqlParameter CharParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Char);
            return param;
        }
        public static SqlParameter NVarCharParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.NVarChar);
            return param;
        }

        public static SqlParameter VarCharParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.VarChar);
            return param;
        }

        public static SqlParameter NTextParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.NText);
            return param;
        }

        // Date and time types
        public static SqlParameter DateTime2Param(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.DateTime2);
            return param;
        }
        public static SqlParameter DateTimeParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.DateTime);
            return param;
        }

        public static SqlParameter DateTimeOffsetParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.DateTimeOffset);
            return param;
        }

        public static SqlParameter DateParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Date);
            return param;
        }

        // Binary and specialized types
        public static SqlParameter VarBinaryParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.VarBinary);
            return param;
        }

        public static SqlParameter BitParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Bit);
            return param;
        }

        public static SqlParameter UniqueIdentifierParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.UniqueIdentifier);
            return param;
        }

        public static SqlParameter XmlParam(string name)
        {
            SqlParameter param = new SqlParameter(name, System.Data.SqlDbType.Xml);
            return param;
        }


    }
}
