using Spearing.Data.Frames;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace BridgeTester1.NewStructure2
{



    public static class DataFrameEvaluator
    {
        public static Func<Row, object> CompileFormula(IExpression expression, Frame context)
        {
            var rowParam = Expression.Parameter(typeof(Row), "row");
            var body = Visit(expression, rowParam, context);

            var convertedBody = Expression.Convert(body, typeof(object));
            return Expression.Lambda<Func<Row, object>>(convertedBody, rowParam).Compile();
        }

        private static Expression Visit(IExpression expr, ParameterExpression rowParam, Frame frame)
        {
            return expr switch
            {
                LiteralExpr e => Expression.Constant(e.Value),
                IdentifierExpr e => BuildColumnAccess(rowParam, e.Name, frame),
                BinaryExpr e => BuildBinary(e, rowParam, frame),
                UnaryExpr e => e.Op == SqlOperator.Sub
                    ? Expression.Negate(Visit(e.Operand, rowParam, frame))
                    : throw new NotSupportedException($"Unary {e.Op} not supported."),
                ConditionalExpr e => BuildConditional(e, rowParam, frame),
                BetweenExpr e => BuildBetween(e, rowParam, frame),
                InExpr e => BuildIn(e, rowParam, frame),
                AggregateExpr e => BuildAggregate(e, rowParam, frame),
                _ => throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported.")
            };
        }

        private static Expression BuildAggregate(AggregateExpr e, ParameterExpression rowParam, Frame frame)
        {
            // 1. Visit all arguments (e.g., Rate1, Rate2) to get their row-level expressions
            var args = e.Arguments.Select(arg => Visit(arg, rowParam, frame)).ToList();

            if (args.Count == 0)
                throw new Exception($"{e.Func} requires at least one argument.");

            // 2. SCALAR PATH: If there are 2+ arguments, it's a row-level comparison (MAX/MIN)
            if (args.Count > 1)
            {
                if (e.Func == AggregateFunc.Max || e.Func == AggregateFunc.Min)
                {
                    return BuildNestedMath(e.Func, args);
                }
                throw new NotSupportedException($"{e.Func} only supports multiple arguments for MAX and MIN.");
            }

            // 3. AGGREGATE PATH: If there is 1 argument, it's a column-level total (SUM, AVG, etc.)
            return BuildStandardAggregate(e.Func, args[0]);
        }

        private static Expression BuildStandardAggregate(AggregateFunc func, Expression argument)
        {
            // In a row-level visitor, a single-argument aggregate acts as a 'Selector'.
            // For example, SUM(Price) returns the expression for 'Price'.

            // Later, your query engine will take this expression and pass it into 
            // a collection method like .Sum(p => p.Price).

            return func switch
            {
                AggregateFunc.Sum => argument,
                AggregateFunc.Max => argument,
                AggregateFunc.Min => argument,
                AggregateFunc.Avg => argument,
                AggregateFunc.Count => argument,
                _ => throw new NotImplementedException($"Aggregate function {func} not implemented.")
            };
        }

        private static Expression BuildNestedMath(AggregateFunc func, List<Expression> args)
        {
            string methodName = func switch
            {
                AggregateFunc.Max => "Max",
                AggregateFunc.Min => "Min",
                _ => throw new NotSupportedException($"The function '{func}' is not supported for multiple arguments.")
            };

            // We use Aggregate to fold the list: (a, b, c) becomes Math.Max(a, Math.Max(b, c))
            return args.Aggregate((accumulator, next) =>
            {
                // FIX: Copy to locals so we can use 'ref'
                var left = accumulator;
                var right = next;

                // Now AlignTypes works because it's hitting local variables
                AlignTypes(ref left, ref right);

                var method = typeof(Math).GetMethod(methodName, new[] { left.Type, right.Type });

                if (method == null)
                {
                    throw new NotSupportedException(
                        $"Math.{methodName} does not support {left.Type.Name}. " +
                        "Check if AlignTypes is promoting to a standard type like Decimal or Double.");
                }

                return Expression.Call(null, method, left, right);
            });
        }


        private static Expression BuildColumnAccess(ParameterExpression rowParam, string colName, Frame frame)
        {
            var column = frame[colName];
            if (column == null || column.Buffer == null)
                throw new Exception($"Column '{colName}' not found in Frame.");

            var method = typeof(Row).GetMethod(nameof(Row.Get))!
                .MakeGenericMethod(column.Buffer.ElementType);

            return Expression.Call(rowParam, method, Expression.Constant(colName));
        }

        private static Expression BuildBinary(BinaryExpr e, ParameterExpression rowParam, Frame frame)
        {
            var left = Visit(e.Left, rowParam, frame);
            var right = Visit(e.Right, rowParam, frame);

            // Special Case: LIKE requires string-specific handling
            if (e.Op == SqlOperator.Like)
            {
                return BuildLike(left, right, e);
            }

            AlignTypes(ref left, ref right);

            return e.Op switch
            {
                SqlOperator.Add => Expression.Add(left, right),
                SqlOperator.Sub => Expression.Subtract(left, right),
                SqlOperator.Mul => Expression.Multiply(left, right),
                SqlOperator.Div => Expression.Divide(left, right),
                SqlOperator.Eq => Expression.Equal(left, right),
                SqlOperator.NotEq => Expression.NotEqual(left, right),
                SqlOperator.Gt => Expression.GreaterThan(left, right),
                SqlOperator.Lt => Expression.LessThan(left, right),
                SqlOperator.Gte => Expression.GreaterThanOrEqual(left, right),
                SqlOperator.Lte => Expression.LessThanOrEqual(left, right),
                SqlOperator.And => Expression.AndAlso(left, right),
                SqlOperator.Or => Expression.OrElse(left, right),
                _ => throw new NotImplementedException($"Operator {e.Op} not implemented.")
            };
        }


        #region LIKE
        private static Expression BuildLike(Expression left, Expression right, BinaryExpr e)
        {
            // Ensure operands are strings
            if (left.Type != typeof(string)) left = Expression.Convert(left, typeof(string));

            // Fast Path: Pattern is a constant string (most common case)
            if (e.Right is LiteralExpr literal && literal.Value is string pattern)
            {
                return BuildOptimizedLike(left, pattern);
            }

            // Slow Path: Pattern is dynamic (e.g. Field1 LIKE Field2)
            if (right.Type != typeof(string)) right = Expression.Convert(right, typeof(string));

            var method = typeof(DataFrameEvaluator).GetMethod(nameof(DataFrameEvaluator.Like), new[] { typeof(string), typeof(string) });
            return Expression.Call(null, method, left, right);
        }
        private static Expression BuildOptimizedLike(Expression member, string pattern)
        {
            bool startsWithWildcard = pattern.StartsWith("%");
            bool endsWithWildcard = pattern.EndsWith("%");
            string cleanPattern = pattern.Trim('%');

            // %pattern% -> .Contains()
            if (startsWithWildcard && endsWithWildcard)
                return CreateStringMethodCall(member, nameof(string.Contains), cleanPattern);

            // pattern% -> .StartsWith()
            if (endsWithWildcard)
                return CreateStringMethodCall(member, nameof(string.StartsWith), cleanPattern);

            // %pattern -> .EndsWith()
            if (startsWithWildcard)
                return CreateStringMethodCall(member, nameof(string.EndsWith), cleanPattern);

            // pattern -> .Equals()
            return Expression.Call(member, typeof(string).GetMethod("Equals", new[] { typeof(string), typeof(StringComparison) }),
                Expression.Constant(cleanPattern), Expression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        private static Expression CreateStringMethodCall(Expression instance, string methodName, string value)
        {
            // StringComparison.OrdinalIgnoreCase makes it behave like standard SQL
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string), typeof(StringComparison) });
            return Expression.Call(instance, method, Expression.Constant(value), Expression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        #endregion


        private static Expression BuildIn(InExpr e, ParameterExpression rowParam, Frame frame)
        {
            var columnExpr = Visit(e.Expression, rowParam, frame);

            // 1. Resolve list of literals
            var literals = e.Values.OfType<LiteralExpr>().Select(l => l.Value).ToList();
            if (literals.Count == 0) return Expression.Constant(false);

            // 2. Determine the "Strongest" Type (Financial/Decimal Precedence)
            Type comparisonType = columnExpr.Type;
            foreach (var lit in literals)
                comparisonType = GetPromotedType(comparisonType, lit.GetType());

            // 3. Create a TYPED ARRAY for the constructor
            // This fixes the MissingMethodException by providing an exact match for IEnumerable<T>
            var typedArray = Array.CreateInstance(comparisonType, literals.Count);
            for (int i = 0; i < literals.Count; i++)
            {
                typedArray.SetValue(Convert.ChangeType(literals[i], comparisonType), i);
            }

            // 4. Create the HashSet<T> using the typed array
            var hashSetType = typeof(HashSet<>).MakeGenericType(comparisonType);
            var hashSet = Activator.CreateInstance(hashSetType, typedArray);

            // 5. Align the column type to the HashSet type
            var alignedColumn = EnsureType(columnExpr, comparisonType);

            // 6. Generate the call: hashSet.Contains(alignedColumnValue)
            var containsMethod = hashSetType.GetMethod("Contains", new[] { comparisonType })!;
            return Expression.Call(Expression.Constant(hashSet), containsMethod, alignedColumn);
        }

        private static Expression BuildConditional(ConditionalExpr e, ParameterExpression rowParam, Frame frame)
        {
            var test = Visit(e.Condition, rowParam, frame);
            var ifTrue = Visit(e.Then, rowParam, frame);
            var ifFalse = Visit(e.Else, rowParam, frame);
            AlignTypes(ref ifTrue, ref ifFalse);
            return Expression.Condition(test, ifTrue, ifFalse);
        }

        private static Expression BuildBetween(BetweenExpr e, ParameterExpression rowParam, Frame frame)
        {
            var val = Visit(e.Expression, rowParam, frame);
            var lower = Visit(e.Lower, rowParam, frame);
            var upper = Visit(e.Upper, rowParam, frame);
            AlignTypes(ref val, ref lower);
            AlignTypes(ref val, ref upper);
            return Expression.AndAlso(Expression.GreaterThanOrEqual(val, lower), Expression.LessThanOrEqual(val, upper));
        }

        private static void AlignTypes(ref Expression left, ref Expression right)
        {
            Type target = GetPromotedType(left.Type, right.Type);
            left = EnsureType(left, target);
            right = EnsureType(right, target);
        }

        private static Type GetPromotedType(Type t1, Type t2)
        {
            if (t1 == t2) return t1;
            // Decimal takes precedence over everything else for financial accuracy
            if (t1 == typeof(decimal) || t2 == typeof(decimal)) return typeof(decimal);
            if (t1 == typeof(double) || t2 == typeof(double)) return typeof(double);
            if (t1 == typeof(float) || t2 == typeof(float)) return typeof(float);
            if (t1 == typeof(long) || t2 == typeof(long)) return typeof(long);
            return typeof(int);
        }

        private static Expression EnsureType(Expression expr, Type targetType)
        {
            return expr.Type == targetType ? expr : Expression.Convert(expr, targetType);
        }

        public static bool Like(string value, string pattern)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern)) return false;

            // If no wildcards, do a simple comparison
            if (!pattern.Contains("%") && !pattern.Contains("_"))
                return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);

            // Convert SQL wildcards to Regex
            // % -> .*
            // _ -> .
            string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                                             .Replace("%", ".*")
                                             .Replace("_", ".") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(
                value,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
            );
        }
    }

    //public static class SqlHelper
    //{
    //    public static bool Like(string value, string pattern)
    //    {
    //        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern)) return false;

    //        // If no wildcards, do a simple comparison
    //        if (!pattern.Contains("%") && !pattern.Contains("_"))
    //            return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);

    //        // Convert SQL wildcards to Regex
    //        // % -> .*
    //        // _ -> .
    //        string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
    //                                         .Replace("%", ".*")
    //                                         .Replace("_", ".") + "$";

    //        return System.Text.RegularExpressions.Regex.IsMatch(
    //            value,
    //            regexPattern,
    //            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
    //        );
    //    }
    //}
}
