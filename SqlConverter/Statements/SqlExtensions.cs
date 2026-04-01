using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Language.SqlConverter.Statements
{
    public static class SqlExtensions
    {
        // Extension for a single Statement node
        public static string ToSql(this Statement stmt)
        {
            if (stmt.Children != null && stmt.Children.Any())
            {
                // Aggregates/Functions: SUM(children)
                if (stmt.Type == StatementType.Aggregate || stmt.Type == StatementType.Function)
                    return $"{stmt.Text}({stmt.Children.ToSql()})";

                // Groups: (children)
                return $"({stmt.Children.ToSql()})";
            }
            return stmt.Text;
        }

        // Extension for an array/list of Statements
        public static string ToSql(this IEnumerable<Statement> statements)
        {
            if (statements == null) return string.Empty;
            return string.Join(" ", statements.Select(s => s.ToSql()));
        }

        // Extension for the entire Parsed Query
        public static string ToSql(this ParsedSqlStatement model)
        {
            var select = "SELECT " + model.SelectStatements.ToSql();
            var from = " FROM " + model.FromTable;
            var where = (model.WhereStatements != null && model.WhereStatements.Any())
                ? " WHERE " + model.WhereStatements.ToSql()
                : "";

            return select + from + where;
        }
    }
}
