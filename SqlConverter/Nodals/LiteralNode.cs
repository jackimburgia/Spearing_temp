using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    //public class LiteralNode : QueryNode
    //{
    //    public string Value { get; set; }
    //    public override string ToSql() => Value;

    //    public override Expression ToExpression(ParameterExpression param)
    //    {
    //        // Simple type inference for literals
    //        if (double.TryParse(Value, out double d)) return Expression.Constant(d);
    //        if (bool.TryParse(Value, out bool b)) return Expression.Constant(b);

    //        // Handle strings by removing SQL quotes
    //        return Expression.Constant(Value.Trim('\''));
    //    }
    //}

    public class LiteralNode : QueryNode
    {
        public string Value { get; set; }
        public override string ToSql() => Value;

        public override Expression ToExpression(ParameterExpression param) => BuildLiteral();
        public override Expression ToExpression(ParameterExpression param, Dictionary<string, Type> schema) => BuildLiteral();

        private Expression BuildLiteral()
        {
            if (double.TryParse(Value, out double d)) return Expression.Constant(d);
            if (bool.TryParse(Value, out bool b)) return Expression.Constant(b);
            return Expression.Constant(Value.Trim('\''));
        }
    }
}
