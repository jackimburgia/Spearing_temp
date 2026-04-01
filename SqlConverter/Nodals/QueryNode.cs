using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    //public abstract class QueryNode
    //{
    //    public abstract string ToSql();

    //    // The core of the Execution Engine
    //    public abstract Expression ToExpression(ParameterExpression param);
    //}

    public abstract class QueryNode
    {
        public abstract string ToSql();

        public abstract Expression ToExpression(ParameterExpression param);

        public abstract Expression ToExpression(ParameterExpression param, Dictionary<string, Type> schema);
    }
}
