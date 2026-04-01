using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    //public class UnaryNode : QueryNode
    //{
    //    public string Operator { get; set; }
    //    public QueryNode Argument { get; set; }

    //    public override string ToSql() => $"{Operator} {Argument.ToSql()}";

    //    public override Expression ToExpression(ParameterExpression param)
    //    {
    //        var arg = Argument.ToExpression(param);
    //        return Operator.ToUpper() switch
    //        {
    //            "NOT" => Expression.Not(arg),
    //            _ => throw new NotSupportedException($"Unary operator '{Operator}' not supported.")
    //        };
    //    }
    //}
    public class UnaryNode : QueryNode
    {
        public string Operator { get; set; }
        public QueryNode Argument { get; set; }

        public override string ToSql() => $"{Operator} {Argument.ToSql()}";

        public override Expression ToExpression(ParameterExpression param)
            => Expression.Not(Argument.ToExpression(param));

        public override Expression ToExpression(ParameterExpression param, Dictionary<string, Type> schema)
            => Expression.Not(Argument.ToExpression(param, schema));
    }
}
