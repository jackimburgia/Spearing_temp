using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    //public class FieldNode : QueryNode
    //{
    //    public string Name { get; set; }
    //    public override string ToSql() => Name;

    //    public override Expression ToExpression(ParameterExpression param)
    //    {
    //        // For strongly typed objects, this finds the property by name
    //        // Example: param.AssetId
    //        return Expression.PropertyOrField(param, Name);
    //    }
    //}

    public class FieldNode : QueryNode
    {
        public string Name { get; set; }
        public override string ToSql() => Name;

        // Mode 1: Standard Reflection (POCOs)
        public override Expression ToExpression(ParameterExpression param)
            => Expression.PropertyOrField(param, Name);

        // Mode 2: Frame Schema Resolution
        public override Expression ToExpression(ParameterExpression param, Dictionary<string, Type> schema)
        {
            if (param.Type.Name == "Row" && schema != null && schema.TryGetValue(Name, out var colType))
            {
                var method = param.Type.GetMethod("Get")?.MakeGenericMethod(colType);
                if (method != null)
                {
                    return Expression.Call(param, method, Expression.Constant(Name));
                }
            }
            return ToExpression(param); // Fallback to Mode 1
        }
    }
}
