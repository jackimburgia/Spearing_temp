using System;
using System.Collections.Generic;
using System.Text;

namespace Spearing.Language.SqlConverter.Statements
{
    public class ParsedSqlStatement
    {
        public Statement[] SelectStatements { get; set; } = Array.Empty<Statement>();
        public string FromTable { get; set; } = string.Empty;
        public Statement[] WhereStatements { get; set; } = Array.Empty<Statement>();
    }
}
