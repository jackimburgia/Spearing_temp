namespace Spearing.Language.SqlConverter.Statements
{
    public class Statement
    {
        public string Text { get; set; }
        public Statement[] Children { get; set; }
        public StatementType Type { get; set; }
    }
}
