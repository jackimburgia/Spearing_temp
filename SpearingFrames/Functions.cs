namespace Spearing.Data.Frames
{
    public static class Functions
    {
        /// <summary>
        /// 'c' is the standard R-style combine function.
        /// Turns a variable list of arguments into a Column.
        /// </summary>
        public static Column c<T>(params T[] values)
        {
            // Uses the high-performance ToColumn() we already built
            return new Column(values.ToColumn());
        }

        /// <summary>
        /// 'col' is an alias for 'c', often used for numeric data.
        /// </summary>
        public static Column col<T>(params T[] values) => c(values);
    }
}
