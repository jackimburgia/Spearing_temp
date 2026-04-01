using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    //public class ConditionalNode : QueryNode
    //{
    //    public QueryNode Condition { get; set; }
    //    public QueryNode ThenValue { get; set; }
    //    public QueryNode ElseValue { get; set; }

    //    public override string ToSql() => $"IF {Condition.ToSql()} THEN {ThenValue.ToSql()} ELSE {ElseValue.ToSql()}";

    //    public override Expression ToExpression(ParameterExpression param)
    //    {
    //        // This is the functional equivalent of the C# ternary: (condition) ? then : else
    //        return Expression.Condition(
    //            Condition.ToExpression(param),
    //            ThenValue.ToExpression(param),
    //            ElseValue.ToExpression(param)
    //        );
    //    }
    //}

    public class ConditionalNode : QueryNode
    {
        public QueryNode Condition { get; set; }
        public QueryNode ThenValue { get; set; }
        public QueryNode ElseValue { get; set; }

        public override string ToSql() => $"IF {Condition.ToSql()} THEN {ThenValue.ToSql()} ELSE {ElseValue.ToSql()}";

        public override Expression ToExpression(ParameterExpression param) =>
            Expression.Condition(Condition.ToExpression(param), ThenValue.ToExpression(param), ElseValue.ToExpression(param));

        public override Expression ToExpression(ParameterExpression param, Dictionary<string, Type> schema) =>
            Expression.Condition(Condition.ToExpression(param, schema), ThenValue.ToExpression(param, schema), ElseValue.ToExpression(param, schema));
    }
}
