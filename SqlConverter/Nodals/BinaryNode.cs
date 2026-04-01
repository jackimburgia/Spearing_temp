using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    //public class BinaryNode : QueryNode
    //{
    //    public QueryNode Left { get; set; }
    //    public string Operator { get; set; }
    //    public QueryNode Right { get; set; }

    //    public override string ToSql() => $"({Left.ToSql()} {Operator} {Right.ToSql()})";

    //    public override Expression ToExpression(ParameterExpression param)
    //    {
    //        var left = Left.ToExpression(param);
    //        var right = Right.ToExpression(param);

    //        return Operator.ToUpper() switch
    //        {
    //            // Arithmetic
    //            "+" => Expression.Add(left, right),
    //            "-" => Expression.Subtract(left, right),
    //            "*" => Expression.Multiply(left, right),
    //            "/" => Expression.Divide(left, right),

    //            // Comparison
    //            "=" => Expression.Equal(left, right),
    //            ">" => Expression.GreaterThan(left, right),
    //            "<" => Expression.LessThan(left, right),
    //            ">=" => Expression.GreaterThanOrEqual(left, right),
    //            "<=" => Expression.LessThanOrEqual(left, right),
    //            "!=" => Expression.NotEqual(left, right),

    //            // Logical Junctions
    //            "AND" => Expression.AndAlso(left, right),
    //            "OR" => Expression.OrElse(left, right),

    //            _ => throw new NotSupportedException($"SQL Operator '{Operator}' is not yet mapped to a C# Expression.")
    //        };
    //    }
    //}

    public class BinaryNode : QueryNode
    {
        public QueryNode Left { get; set; }
        public string Operator { get; set; }
        public QueryNode Right { get; set; }

        public override string ToSql() => $"({Left.ToSql()} {Operator} {Right.ToSql()})";

        public override Expression ToExpression(ParameterExpression param) =>
            BuildExpression(Left.ToExpression(param), Right.ToExpression(param));

        public override Expression ToExpression(ParameterExpression param, Dictionary<string, Type> schema)
        {
            var left = Left.ToExpression(param, schema);
            var right = Right.ToExpression(param, schema);

            // 1. Handle Numeric Promotion (e.g., int * double)
            // This prevents InvalidOperationException when types don't match exactly
            if (left.Type != right.Type && IsNumeric(left.Type) && IsNumeric(right.Type))
            {
                if (left.Type == typeof(double)) right = Expression.Convert(right, typeof(double));
                else if (right.Type == typeof(double)) left = Expression.Convert(left, typeof(double));
                else if (left.Type == typeof(decimal)) right = Expression.Convert(right, typeof(decimal));
                else if (right.Type == typeof(decimal)) left = Expression.Convert(left, typeof(decimal));
            }

            // 2. Handle SQL String Concatenation using '+'
            if (Operator == "+" && (left.Type == typeof(string) || right.Type == typeof(string)))
            {
                var concat = typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object) });
                return Expression.Call(concat!,
                    Expression.Convert(left, typeof(object)),
                    Expression.Convert(right, typeof(object)));
            }

            return BuildExpression(left, right);
        }

        private bool IsNumeric(Type t) =>
            t == typeof(int) || t == typeof(double) || t == typeof(long) ||
            t == typeof(float) || t == typeof(decimal) || t == typeof(short);

        private Expression BuildExpression(Expression left, Expression right)
        {
            return Operator.ToUpper() switch
            {
                "+" => Expression.Add(left, right),
                "-" => Expression.Subtract(left, right),
                "*" => Expression.Multiply(left, right),
                "/" => Expression.Divide(left, right),
                "=" => Expression.Equal(left, right),
                ">" => Expression.GreaterThan(left, right),
                "<" => Expression.LessThan(left, right),
                ">=" => Expression.GreaterThanOrEqual(left, right),
                "<=" => Expression.LessThanOrEqual(left, right),
                "!=" => Expression.NotEqual(left, right),
                "AND" => Expression.AndAlso(left, right),
                "OR" => Expression.OrElse(left, right),
                _ => throw new NotSupportedException($"SQL Operator '{Operator}' is not supported.")
            };
        }
    }
}
