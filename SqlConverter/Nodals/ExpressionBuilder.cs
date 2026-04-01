using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    public static class ExpressionBuilder
    {
        // Creates a Lambda for the WHERE clause: x => x.Price > 100
        public static Expression<Func<T, bool>> BuildPredicate<T>(QueryNode whereRoot)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var body = whereRoot.ToExpression(parameter);

            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        // Creates a Lambda for a single SELECT column: x => x.Par * x.Price
        public static Expression<Func<T, object>> BuildSelector<T>(QueryNode columnRoot)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var body = columnRoot.ToExpression(parameter);

            // We box the result to 'object' so it works for any return type
            var conversion = Expression.Convert(body, typeof(object));
            return Expression.Lambda<Func<T, object>>(conversion, parameter);
        }
    }
}
