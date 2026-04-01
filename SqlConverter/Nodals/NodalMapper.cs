

using Spearing.Language.SqlConverter.Statements;

namespace Spearing.Language.SqlConverter.Nodals
{


    public static class NodalMapper
    {
        public static SqlQueryModel Map(ParsedSqlStatement dissected)
        {
            return new SqlQueryModel
            {
                FromTable = dissected.FromTable,
                Columns = MapColumns(dissected.SelectStatements),
                WhereRoot = MapExpressionChain(dissected.WhereStatements)
            };
        }

        private static List<NodalColumn> MapColumns(Statement[] statements)
        {
            var columns = new List<NodalColumn>();
            var currentParts = new List<Statement>();

            foreach (var stmt in statements)
            {
                if (stmt.Type == StatementType.Separator)
                {
                    if (currentParts.Any()) columns.Add(ProcessColumn(currentParts));
                    currentParts.Clear();
                }
                else currentParts.Add(stmt);
            }
            if (currentParts.Any()) columns.Add(ProcessColumn(currentParts));
            return columns;
        }

        private static NodalColumn ProcessColumn(List<Statement> parts)
        {
            var col = new NodalColumn();
            if (parts.Any() && parts.Last().Type == StatementType.Alias)
            {
                col.Alias = parts.Last().Text.Replace("AS ", "", StringComparison.OrdinalIgnoreCase).Trim();
                parts.RemoveAt(parts.Count - 1);
            }
            col.RootNode = MapExpressionChain(parts.ToArray());
            if (string.IsNullOrEmpty(col.Alias)) col.Alias = col.RootNode?.ToSql() ?? "Unknown";
            return col;
        }

        // --- PRECEDENCE CHAIN: Ensures AND/OR/Comparisons are handled as BinaryNodes ---
        public static QueryNode MapExpressionChain(Statement[] statements)
        {
            if (statements == null || !statements.Any()) return null;
            return ParseOr(statements.ToList());
        }

        private static QueryNode ParseOr(List<Statement> s) => ParseLevel(s, new[] { "OR" }, ParseAnd);
        private static QueryNode ParseAnd(List<Statement> s) => ParseLevel(s, new[] { "AND" }, ParseComparison);
        private static QueryNode ParseComparison(List<Statement> s) => ParseLevel(s, new[] { "=", ">", "<", ">=", "<=", "!=" }, ParseAddition);
        private static QueryNode ParseAddition(List<Statement> s) => ParseLevel(s, new[] { "+", "-" }, ParseMultiplication);
        private static QueryNode ParseMultiplication(List<Statement> s) => ParseLevel(s, new[] { "*", "/" }, ParsePrimary);

        private static QueryNode ParseLevel(List<Statement> stmts, string[] ops, Func<List<Statement>, QueryNode> nextLevel)
        {
            for (int i = stmts.Count - 1; i >= 0; i--)
            {
                // We only split if it's a top-level operator (not inside a function/group)
                if (ops.Contains(stmts[i].Text.ToUpper()) && stmts[i].Children == null)
                {
                    return new BinaryNode
                    {
                        Left = ParseLevel(stmts.Take(i).ToList(), ops, nextLevel), // Handle left-associativity
                        Operator = stmts[i].Text,
                        Right = nextLevel(stmts.Skip(i + 1).ToList())
                    };
                }
            }
            return nextLevel(stmts);
        }

        private static QueryNode ParsePrimary(List<Statement> stmts)
        {
            int i = 0;
            return MapRecursive(stmts.ToArray(), ref i);
        }

        private static QueryNode MapRecursive(Statement[] stmts, ref int i)
        {
            if (i >= stmts.Length) return null;
            var current = stmts[i];

            // 1. IF/THEN/ELSE Logic (Properly calling MapExpressionChain for each block)
            if (current.Type == StatementType.If)
            {
                i++;
                var condition = MapExpressionChain(GetUntil(stmts, ref i, StatementType.Then));
                if (i < stmts.Length && stmts[i].Type == StatementType.Then) i++;

                var thenVal = MapExpressionChain(GetUntil(stmts, ref i, StatementType.Else, StatementType.ElseIf));

                QueryNode elseVal = null;
                if (i < stmts.Length && (stmts[i].Type == StatementType.Else || stmts[i].Type == StatementType.ElseIf))
                {
                    // If it's ELSE IF, we convert it to an IF for recursion
                    if (stmts[i].Type == StatementType.Else) i++;
                    else stmts[i].Type = StatementType.If;

                    elseVal = MapExpressionChain(stmts.Skip(i).ToArray());
                    i = stmts.Length;
                }
                return new ConditionalNode { Condition = condition, ThenValue = thenVal, ElseValue = elseVal };
            }

            // 2. Groups / Functions
            if (current.Type == StatementType.LogicalGroup) return MapExpressionChain(current.Children);
            if (current.Type == StatementType.Function || current.Type == StatementType.Aggregate)
            {
                var args = new List<QueryNode>();
                if (current.Children != null)
                {
                    int childIdx = 0;
                    while (childIdx < current.Children.Length)
                    {
                        var argParts = GetUntil(current.Children, ref childIdx, StatementType.Separator);
                        if (argParts.Any()) args.Add(MapExpressionChain(argParts));
                        if (childIdx < current.Children.Length && current.Children[childIdx].Type == StatementType.Separator) childIdx++;
                    }
                }
                return new FunctionNode { Name = current.Text, Arguments = args };
            }

            // 3. Terminals
            i++;
            if (current.Type == StatementType.Field) return new FieldNode { Name = current.Text };
            return new LiteralNode { Value = current.Text };
        }

        private static Statement[] GetUntil(Statement[] stmts, ref int i, params StatementType[] types)
        {
            var list = new List<Statement>();
            while (i < stmts.Length && !types.Contains(stmts[i].Type)) list.Add(stmts[i++]);
            return list.ToArray();
        }
    }
    //public static class NodalMapper
    //{
    //    public static SqlQueryModel Map(ParsedSqlStatement dissected)
    //    {
    //        return new SqlQueryModel
    //        {
    //            FromTable = dissected.FromTable,
    //            Columns = MapColumns(dissected.SelectStatements),
    //            WhereRoot = MapExpressionChain(dissected.WhereStatements)
    //        };
    //    }

    //    private static List<NodalColumn> MapColumns(Statement[] statements)
    //    {
    //        var columns = new List<NodalColumn>();
    //        var currentParts = new List<Statement>();

    //        foreach (var stmt in statements)
    //        {
    //            if (stmt.Type == StatementType.Separator)
    //            {
    //                if (currentParts.Any()) columns.Add(ProcessColumn(currentParts));
    //                currentParts.Clear();
    //            }
    //            else currentParts.Add(stmt);
    //        }
    //        if (currentParts.Any()) columns.Add(ProcessColumn(currentParts));
    //        return columns;
    //    }

    //    private static NodalColumn ProcessColumn(List<Statement> parts)
    //    {
    //        var col = new NodalColumn();
    //        if (parts.Any() && parts.Last().Type == StatementType.Alias)
    //        {
    //            col.Alias = parts.Last().Text.Replace("AS ", "", StringComparison.OrdinalIgnoreCase).Trim();
    //            parts.RemoveAt(parts.Count - 1);
    //        }

    //        col.RootNode = MapExpressionChain(parts.ToArray());
    //        if (string.IsNullOrEmpty(col.Alias)) col.Alias = col.RootNode?.ToSql() ?? "Unknown";
    //        return col;
    //    }

    //    public static QueryNode MapExpressionChain(Statement[] statements)
    //    {
    //        if (statements == null || !statements.Any()) return null;

    //        int i = 0;
    //        QueryNode root = MapRecursive(statements, ref i);

    //        // Chain binary operations: (A + B) + C
    //        while (i < statements.Length)
    //        {
    //            var current = statements[i];
    //            if (current.Type == StatementType.Arithmetic ||
    //                current.Type == StatementType.Comparison ||
    //                current.Type == StatementType.LogicalJunction)
    //            {
    //                i++;
    //                var right = MapRecursive(statements, ref i);
    //                root = new BinaryNode { Left = root, Operator = current.Text, Right = right };
    //            }
    //            else i++;
    //        }
    //        return root;
    //    }

    //    private static QueryNode MapRecursive(Statement[] stmts, ref int i)
    //    {
    //        if (i >= stmts.Length) return null;
    //        var current = stmts[i];

    //        // 1. Logic (IF/THEN/ELSE)
    //        if (current.Type == StatementType.If)
    //        {
    //            i++;
    //            var condition = MapRecursive(stmts, ref i);
    //            if (i < stmts.Length && stmts[i].Type == StatementType.Then) i++;
    //            var thenVal = MapRecursive(stmts, ref i);
    //            if (i < stmts.Length && stmts[i].Type == StatementType.Else) i++;
    //            var elseVal = MapRecursive(stmts, ref i);
    //            return new ConditionalNode { Condition = condition, ThenValue = thenVal, ElseValue = elseVal };
    //        }

    //        // 2. Aggregates/Functions (Fixing the double-increment bug here)
    //        if (current.Type == StatementType.Aggregate || current.Type == StatementType.Function)
    //        {
    //            i++;
    //            var args = new List<QueryNode>();
    //            if (current.Children != null)
    //            {
    //                int childIdx = 0;
    //                while (childIdx < current.Children.Length)
    //                {
    //                    var arg = MapRecursive(current.Children, ref childIdx);
    //                    if (arg != null) args.Add(arg);

    //                    // Only jump ahead if we hit an explicit comma
    //                    if (childIdx < current.Children.Length && current.Children[childIdx].Type == StatementType.Separator)
    //                        childIdx++;
    //                }
    //            }
    //            return new FunctionNode { Name = current.Text, Arguments = args };
    //        }

    //        // 3. Parentheses Groups
    //        if (current.Type == StatementType.LogicalGroup)
    //        {
    //            i++;
    //            return MapExpressionChain(current.Children);
    //        }

    //        // 4. Unary (NOT)
    //        if (current.Text.ToUpper() == "NOT")
    //        {
    //            i++;
    //            return new UnaryNode { Operator = "NOT", Argument = MapRecursive(stmts, ref i) };
    //        }

    //        // 5. Terminals
    //        i++;
    //        if (current.Type == StatementType.Field) return new FieldNode { Name = current.Text };
    //        return new LiteralNode { Value = current.Text };
    //    }
    //}
}
