using System.Text.RegularExpressions;

namespace Spearing.Language.SqlConverter.Statements
{
    public static class StatementParser
    {
        public static Statement[] Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<Statement>();
            string[] atoms = TokenizeToAtoms(input);
            int index = 0;
            return ParseRecursive(atoms, ref index);
        }

        private static Statement[] ParseRecursive(string[] atoms, ref int i)
        {
            var result = new List<Statement>();

            while (i < atoms.Length)
            {
                if (atoms[i] == ")") break;
                var slice = atoms.Skip(i).ToArray();

                Statement stmt = slice switch
                {
                    ["(", ..] => ConsumeGroup(atoms, ref i),
                    [",", ..] => ConsumeLeaf(ref i, 1, ",", StatementType.Separator),

                    // 1. Conditional Keywords
                    ["IF" or "if", ..] => ConsumeLeaf(ref i, 1, "IF", StatementType.If),
                    ["THEN" or "then", ..] => ConsumeLeaf(ref i, 1, "THEN", StatementType.Then),
                    ["ELSE" or "else", "IF" or "if", ..] => ConsumeLeaf(ref i, 2, "ELSE IF", StatementType.ElseIf),
                    ["ELSE" or "else", ..] => ConsumeLeaf(ref i, 1, "ELSE", StatementType.Else),

                    // 2. Functions & Aggregates
                    [var func, "(", ..] when IsAggregate(func) => ConsumeFunction(func, atoms, ref i, StatementType.Aggregate),
                    [var func, "(", ..] when IsFunction(func) => ConsumeFunction(func, atoms, ref i, StatementType.Function),

                    // 3. Specialty SQL Operators (Preserving your original specialty logic)
                    [var f, "IN" or "in", "(", ..] => ConsumeInClause(f, atoms, ref i),
                    ["AND" or "and" or "OR" or "or", ..] => ConsumeLeaf(ref i, 1, atoms[i].ToUpper(), StatementType.LogicalJunction),

                    // 4. Base Operators (Comparison & Math)
                    [var op, ..] when IsComparisonOp(op) => ConsumeLeaf(ref i, 1, op, StatementType.Comparison),
                    [var op, ..] when IsMathOp(op) => ConsumeLeaf(ref i, 1, op, StatementType.Arithmetic),

                    // 5. Aliasing
                    ["AS" or "as", var name, ..] => ConsumeLeaf(ref i, 2, $"AS {name}", StatementType.Alias),

                    // 6. Terminals (Fallback)
                    _ => ConsumeLeaf(ref i, 1, atoms[i], GetTerminalType(atoms[i]))
                };

                if (stmt != null) result.Add(stmt);
            }
            return result.ToArray();
        }

        private static Statement ConsumeGroup(string[] atoms, ref int i)
        {
            i++;
            var children = ParseRecursive(atoms, ref i);
            if (i < atoms.Length && atoms[i] == ")") i++;
            return new Statement { Children = children, Type = StatementType.LogicalGroup };
        }

        private static Statement ConsumeFunction(string name, string[] atoms, ref int i, StatementType type)
        {
            i += 2;
            var children = ParseRecursive(atoms, ref i);
            if (i < atoms.Length && atoms[i] == ")") i++;
            return new Statement { Text = name.ToUpper(), Children = children, Type = type };
        }

        private static Statement ConsumeInClause(string field, string[] atoms, ref int i)
        {
            i += 3;
            var values = new List<string>();
            while (i < atoms.Length && atoms[i] != ")")
            {
                if (atoms[i] != ",") values.Add(atoms[i]);
                i++;
            }
            if (i < atoms.Length && atoms[i] == ")") i++;
            return new Statement { Text = field, Children = values.Select(v => new Statement { Text = v, Type = GetTerminalType(v) }).ToArray(), Type = StatementType.Set };
        }

        private static Statement ConsumeLeaf(ref int i, int count, string text, StatementType type)
        {
            i += count;
            return new Statement { Text = text, Type = type };
        }

        private static bool IsComparisonOp(string op) => op is "=" or ">" or "<" or ">=" or "<=" or "!=";
        private static bool IsMathOp(string op) => op is "+" or "-" or "*" or "/";
        private static bool IsAggregate(string n) => new[] { "SUM", "MAX", "MIN", "COUNT", "AVG" }.Contains(n.ToUpper());
        private static bool IsFunction(string n) => new[] { "ABS", "ROUND" }.Contains(n.ToUpper());

        private static StatementType GetTerminalType(string token)
        {
            if (token.StartsWith("'") && token.EndsWith("'")) return StatementType.Literal;
            if (double.TryParse(token, out _)) return StatementType.Literal;
            return StatementType.Field;
        }

        private static string[] TokenizeToAtoms(string input)
        {
            var pattern = @"'[^']*'|[(),]|[^(),\s]+";
            return Regex.Matches(input, pattern).Cast<Match>().Select(m => m.Value).ToArray();
        }
    }

    //public static class StatementParser
    //{
    //    public static Statement[] Parse(string input)
    //    {
    //        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<Statement>();

    //        string[] atoms = TokenizeToAtoms(input);
    //        int index = 0;

    //        return ParseRecursive(atoms, ref index);
    //    }

    //    private static Statement[] ParseRecursive(string[] atoms, ref int i)
    //    {
    //        var result = new List<Statement>();

    //        while (i < atoms.Length)
    //        {
    //            if (atoms[i] == ")") break;

    //            var slice = atoms.Skip(i).ToArray();

    //            Statement stmt = slice switch
    //            {
    //                // 1. Structural Elements
    //                ["(", ..] => ConsumeGroup(atoms, ref i),
    //                [",", ..] => ConsumeLeaf(ref i, 1, ",", StatementType.Separator),

    //                // 2. Specialized SQL Comparisons (IS NULL / IS NOT NULL)
    //                [var f, "IS" or "is", "NOT" or "not", "NULL" or "null", ..] =>
    //                    ConsumeLeaf(ref i, 4, $"{f} IS NOT NULL", StatementType.Comparison),

    //                [var f, "IS" or "is", "NULL" or "null", ..] =>
    //                    ConsumeLeaf(ref i, 3, $"{f} IS NULL", StatementType.Comparison),

    //                // 3. Functions & Aggregates
    //                [var func, "(", ..] when IsAggregate(func) =>
    //                    ConsumeFunction(func, atoms, ref i, StatementType.Aggregate),

    //                [var func, "(", ..] when IsFunction(func) =>
    //                    ConsumeFunction(func, atoms, ref i, StatementType.Function),

    //                // 4. Conditional Keywords
    //                ["IF" or "if", ..] => ConsumeLeaf(ref i, 1, "IF", StatementType.If),
    //                ["THEN" or "then", ..] => ConsumeLeaf(ref i, 1, "THEN", StatementType.Then),
    //                ["ELSE" or "else", "IF" or "if", ..] => ConsumeLeaf(ref i, 2, "ELSE IF", StatementType.ElseIf),
    //                ["ELSE" or "else", ..] => ConsumeLeaf(ref i, 1, "ELSE", StatementType.Else),

    //                // 5. Aliasing (AS Name)
    //                ["AS" or "as", var name, ..] when IsValue(name) =>
    //                    ConsumeLeaf(ref i, 2, $"AS {name}", StatementType.Alias),

    //                // 6. SQL Range & Set
    //                [var f, "BETWEEN" or "between", var l, "AND" or "and", var h, ..] =>
    //                    ConsumeLeaf(ref i, 5, $"{f} BETWEEN {l} AND {h}", StatementType.Range),
    //                [var f, "IN" or "in", "(", ..] => ConsumeInClause(f, atoms, ref i),

    //                // 7. Trio Operations (Math & Comparison)
    //                [var f, var op, var v, ..] when IsMathOp(op) && IsValue(f) && IsValue(v) =>
    //                    ConsumeLeaf(ref i, 3, $"{f} {op} {v}", StatementType.Arithmetic),
    //                [var f, var op, var v, ..] when IsComparisonOp(op) && IsValue(f) && IsValue(v) =>
    //                    ConsumeLeaf(ref i, 3, $"{f} {op} {v}", StatementType.Comparison),

    //                // 8. Standalone Operators / Junctions / Keywords
    //                ["DISTINCT" or "distinct", ..] => ConsumeLeaf(ref i, 1, "DISTINCT", StatementType.Literal),
    //                [var op, ..] when IsMathOp(op) => ConsumeLeaf(ref i, 1, op, StatementType.Arithmetic),
    //                [var op, ..] when IsComparisonOp(op) => ConsumeLeaf(ref i, 1, op, StatementType.Comparison),
    //                ["AND" or "and" or "OR" or "or", ..] => ConsumeLeaf(ref i, 1, atoms[i].ToUpper(), StatementType.LogicalJunction),
    //                ["AS" or "as", ..] => ConsumeLeaf(ref i, 1, "AS", StatementType.Alias),


    //                // 9. Fallback
    //                _ => ConsumeLeaf(ref i, 1, atoms[i], GetTerminalType(atoms[i]))
    //            };

    //            if (stmt != null) result.Add(stmt);
    //        }

    //        return result.ToArray();
    //    }

    //    private static Statement ConsumeGroup(string[] atoms, ref int i)
    //    {
    //        i++;
    //        var children = ParseRecursive(atoms, ref i);
    //        if (i < atoms.Length && atoms[i] == ")") i++;
    //        return new Statement { Children = children, Type = StatementType.LogicalGroup };
    //    }

    //    private static Statement ConsumeFunction(string name, string[] atoms, ref int i, StatementType type)
    //    {
    //        i++; // Skip Name
    //        i++; // Skip '('
    //        var children = ParseRecursive(atoms, ref i);
    //        if (i < atoms.Length && atoms[i] == ")") i++; // Skip ')'

    //        return new Statement { Text = name.ToUpper(), Children = children, Type = type };
    //    }

    //    private static Statement ConsumeInClause(string field, string[] atoms, ref int i)
    //    {
    //        i += 3;
    //        var values = new List<string>();
    //        while (i < atoms.Length && atoms[i] != ")")
    //        {
    //            if (atoms[i] != ",") values.Add(atoms[i]);
    //            i++;
    //        }
    //        if (i < atoms.Length && atoms[i] == ")") i++;
    //        return new Statement { Text = $"{field} IN ({string.Join(", ", values)})", Type = StatementType.Set };
    //    }

    //    private static Statement ConsumeLeaf(ref int i, int count, string text, StatementType type)
    //    {
    //        i += count;
    //        return new Statement { Text = text, Type = type };
    //    }

    //    private static bool IsComparisonOp(string op) => op is "=" or ">" or "<" or ">=" or "<=" or "!=" or "LIKE" or "like";
    //    private static bool IsMathOp(string op) => op is "+" or "-" or "*" or "/" or "%" or "^";
    //    private static bool IsAggregate(string n) => new[] { "SUM", "MAX", "MIN", "COUNT", "AVG" }.Contains(n.ToUpper());
    //    private static bool IsFunction(string n) => new[] { "ABS", "ROUND", "TRUNC", "SUBSTRING" }.Contains(n.ToUpper());

    //    private static bool IsValue(string v) =>
    //        v != "(" && v != ")" && v != "," && v != "*" &&
    //        !new[] { "AND", "OR", "BETWEEN", "IN", "IF", "THEN", "ELSE", "AS", "DISTINCT", "IS", "NOT", "NULL" }
    //        .Contains(v.ToUpper());

    //    private static StatementType GetTerminalType(string token)
    //    {
    //        if (token.StartsWith("'") && token.EndsWith("'")) return StatementType.Literal;
    //        if (double.TryParse(token, out _)) return StatementType.Literal;
    //        if (token.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return StatementType.Literal;
    //        return StatementType.Field;
    //    }

    //    private static string[] TokenizeToAtoms(string input)
    //    {
    //        var pattern = @"'[^']*'|[(),]|[^(),\s]+";
    //        return Regex.Matches(input, pattern).Cast<Match>().Select(m => m.Value).ToArray();
    //    }
    //}
}
