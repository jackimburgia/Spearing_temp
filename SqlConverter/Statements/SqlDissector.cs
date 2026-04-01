using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Spearing.Language.SqlConverter.Statements
{
    public static class SqlDissector
    {
        public static ParsedSqlStatement Dissect(string sql)
        {
            var model = new ParsedSqlStatement();

            // 1. SELECT: Capture everything between SELECT and FROM
            // Uses a "positive lookahead" to find FROM without including it in the match
            var selectMatch = Regex.Match(sql, @"SELECT\s+(.*?)(?=\s+FROM|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // 2. FROM: Capture the single word (table name) immediately after FROM
            var fromMatch = Regex.Match(sql, @"FROM\s+([^\s\n;]+)", RegexOptions.IgnoreCase);

            // 3. WHERE: Capture everything after the WHERE keyword
            var whereMatch = Regex.Match(sql, @"WHERE\s+(.*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Process SELECT
            if (selectMatch.Success)
            {
                var content = selectMatch.Groups[1].Value.Trim();
                model.SelectStatements = StatementParser.Parse(content);
            }

            // Process FROM
            if (fromMatch.Success)
            {
                model.FromTable = fromMatch.Groups[1].Value.Trim();
            }

            // Process WHERE (Optional)
            if (whereMatch.Success)
            {
                var content = whereMatch.Groups[1].Value.Trim();
                model.WhereStatements = StatementParser.Parse(content);
            }

            return model;
        }
    }
}
