namespace Spearing.Language.SqlConverter.Nodals
{
    // Master Container for the Full Statement
    public class SqlQueryModel
    {
        public List<NodalColumn> Columns { get; set; } = new();
        public string FromTable { get; set; }
        public QueryNode WhereRoot { get; set; }
    }
}
