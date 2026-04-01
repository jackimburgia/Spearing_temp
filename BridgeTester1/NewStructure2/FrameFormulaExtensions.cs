using Spearing.Data.Frames;

namespace BridgeTester1.NewStructure2
{


    public static class FrameFormulaExtensions
    {

        public static (Column column, WhereStatement where) ToColumnAll(this Frame frame, string formula)
        {
            SqlParser parser = new SqlParser(formula);
            WhereStatement where = parser.ParseWhere();

            IExpression expression = where.Expression;
            Func<Row, object> compiled = DataFrameEvaluator.CompileFormula(expression, frame);

            int rowCount = frame.RowCount;
            object[] results = new object[rowCount];

            for (int i = 0; i < rowCount; i++)
            {
                results[i] = compiled(frame[i]);
            }

            // --- THE FIX ---
            // Instead of passing the object[] directly, we convert it to 
            // a typed buffer (e.g., int[], double[], bool[], etc.)
            IColumnBuffer typedBuffer = InferAndConvert(results);

            Column column = new Column(typedBuffer);

            return (column, where);
        }

        public static Column ToColumn(this Frame frame, string formula)
        {
            (Column column, _) = frame.ToColumnAll(formula);
            return column;
        }

        private static IColumnBuffer InferAndConvert(object[] data)
        {
            if (data.Length == 0) return data.ToColumn(); // Fallback to object

            // 1. Identify the most common/restrictive type in the result set
            // (Ignoring nulls for the initial type check)
            var firstNonNull = data.FirstOrDefault(x => x != null);
            if (firstNonNull == null) return data.ToColumn(); // All nulls? Stay object/nullable

            Type targetType = firstNonNull.GetType();
            bool hasNulls = data.Any(x => x == null);

            // 2. If it's a value type and contains nulls, we need Nullable<T>
            if (hasNulls && targetType.IsValueType)
            {
                targetType = typeof(Nullable<>).MakeGenericType(targetType);
            }

            // 3. Create the specialized array (e.g., bool[], decimal?[])
            Array typedArray = Array.CreateInstance(targetType, data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                typedArray.SetValue(data[i], i);
            }

            // 4. Wrap it in your high-performance ColumnBuffer
            return typedArray.ToColumn();
        }


        #region TODO - retire

        public static void SetColumn(this Frame frame, string columnName, string formula)
        {
            SqlParser parser = new SqlParser(formula);
            WhereStatement where = parser.ParseWhere();

            IExpression expression = where.Expression;
            Func<Row, object> compiled = DataFrameEvaluator.CompileFormula(expression, frame);

            int rowCount = frame.RowCount;
            object[] results = new object[rowCount];

            for (int i = 0; i < rowCount; i++)
            {
                results[i] = compiled(frame[i]);
            }

            // --- THE FIX ---
            // Instead of passing the object[] directly, we convert it to 
            // a typed buffer (e.g., int[], double[], bool[], etc.)
            IColumnBuffer typedBuffer = InferAndConvert(results);

            frame[columnName] = new Column(typedBuffer);

            //frame.AddColumn(columnName, typedBuffer);
        }

        #endregion




    }
}
