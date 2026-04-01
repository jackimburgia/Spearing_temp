namespace Spearing.Language.SqlConverter.Nodals
{
    public static class NodalExtensions
    {
        public static string ToSql(this SqlQueryModel model)
        {
            var select = "SELECT " + string.Join(", ", model.Columns.Select(c =>
                c.Alias == c.RootNode.ToSql() ? c.RootNode.ToSql() : $"{c.RootNode.ToSql()} AS {c.Alias}"));

            var from = " FROM " + model.FromTable;

            var where = model.WhereRoot != null
                ? " WHERE " + model.WhereRoot.ToSql()
                : "";

            return select + from + where;
        }
    }
}
