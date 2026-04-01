using System.Linq.Expressions;

namespace Spearing.Language.SqlConverter.Nodals
{
    //public class FunctionNode : QueryNode
    //{
    //    public string Name { get; set; }
    //    public List<QueryNode> Arguments { get; set; } = new();

    //    public override string ToSql()
    //    {
    //        var formattedArgs = new List<string>();

    //        for (int i = 0; i < Arguments.Count; i++)
    //        {
    //            string currentSql = Arguments[i].ToSql();

    //            // Check if this argument is the DISTINCT modifier
    //            if (currentSql.Trim().Equals("DISTINCT", StringComparison.OrdinalIgnoreCase)
    //                && i + 1 < Arguments.Count)
    //            {
    //                // Merge DISTINCT with the next argument (e.g., "DISTINCT AssetId")
    //                formattedArgs.Add($"DISTINCT {Arguments[i + 1].ToSql()}");
    //                i++; // Skip the next argument since we just consumed it
    //            }
    //            else
    //            {
    //                formattedArgs.Add(currentSql);
    //            }
    //        }

    //        return $"{Name}({string.Join(", ", formattedArgs)})";
    //    }

    //    // This will be used for the Evaluator phase
    //    public override Expression ToExpression(ParameterExpression param)
    //    {
    //        // ... (existing logic) ...
    //        return Expression.Empty();
    //    }
    //}

    public class FunctionNode : QueryNode
    {
        public string Name { get; set; }
        public List<QueryNode> Arguments { get; set; } = new();

        public override string ToSql()
        {
            var formattedArgs = new List<string>();

            for (int i = 0; i < Arguments.Count; i++)
            {
                string currentSql = Arguments[i].ToSql();

                // Check if this argument is the DISTINCT modifier
                if (currentSql.Trim().Equals("DISTINCT", StringComparison.OrdinalIgnoreCase)
                    && i + 1 < Arguments.Count)
                {
                    // Merge DISTINCT with the next argument (e.g., "DISTINCT AssetId")
                    formattedArgs.Add($"DISTINCT {Arguments[i + 1].ToSql()}");
                    i++; // Skip the next argument since we just consumed it
                }
                else
                {
                    formattedArgs.Add(currentSql);
                }
            }

            return $"{Name}({string.Join(", ", formattedArgs)})";
        }

        // Mode 1: Standard Signature for existing Unit Tests
        public override Expression ToExpression(ParameterExpression param)
        {
            // Placeholder: Returning Empty to maintain original behavior 
            // until specific functions (SUM, ABS, etc.) are mapped.
            return Expression.Empty();
        }

        // Mode 2: Frame Schema Signature
        public override Expression ToExpression(ParameterExpression param, Dictionary<string, Type> schema)
        {
            // We still need to ensure that when we eventually implement this,
            // we pass the schema down to any arguments that might be FieldNodes.
            foreach (var arg in Arguments)
            {
                // arg.ToExpression(param, schema); 
            }

            return Expression.Empty();
        }
    }
}
