using Spearing.Data.Frames;
using Spearing.Language.SqlConverter.Nodals;
using Spearing.Language.SqlConverter.Statements;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace BridgeTester1
{
    public static class FrameEvaluator
    {
        public static Frame Query(this Frame source, string sql)
        {
            // 1. Map SQL to the Nodal Model
            var dissected = SqlDissector.Dissect(sql);
            var queryModel = NodalMapper.Map(dissected);

            // 2. Build the Schema using the public indexer to avoid protection level errors
            var schema = source.ColumnNames.ToDictionary(
                name => name,
                name => source[name].Buffer.ElementType, // Accesses public Column.Buffer.ElementType
                StringComparer.OrdinalIgnoreCase
            );

            // 3. Filtering (WHERE)
            Frame workFrame = source;
            if (queryModel.WhereRoot != null)
            {
                workFrame = ApplyFilter(source, queryModel.WhereRoot, schema);
            }

            // 4. Projection (SELECT)
            return ApplyProjection(workFrame, queryModel.Columns, schema);
        }

        private static Frame ApplyFilter(Frame source, QueryNode whereRoot, Dictionary<string, Type> schema)
        {
            var param = Expression.Parameter(typeof(Row), "r");
            var body = whereRoot.ToExpression(param, schema);
            var predicate = Expression.Lambda<Func<Row, bool>>(body, param).Compile();

            var indices = new List<int>();
            for (int i = 0; i < source.RowCount; i++)
            {
                if (predicate(source[i])) indices.Add(i);
            }

            var result = new Frame(indices.Count);
            var indexArray = indices.ToArray();
            foreach (var name in source.ColumnNames)
            {
                // Uses the public IColumnBuffer.Gather method
                result[name] = new Column(source[name].Buffer.Gather(indexArray));
            }
            return result;
        }

        private static Frame ApplyProjection(Frame source, List<NodalColumn> columns, Dictionary<string, Type> schema)
        {
            var resultFrame = new Frame(source.RowCount);
            var param = Expression.Parameter(typeof(Row), "r");

            foreach (var col in columns)
            {
                var body = col.RootNode.ToExpression(param, schema);

                // Box to object for the heterogeneous result array
                var conversion = Expression.Convert(body, typeof(object));
                var selector = Expression.Lambda<Func<Row, object>>(conversion, param).Compile();

                var columnData = new object[source.RowCount];
                for (int i = 0; i < source.RowCount; i++)
                {
                    columnData[i] = selector(source[i]);
                }

                // AddColumn dynamically creates the typed buffer via reflection
                resultFrame.AddColumn(col.Alias, columnData);
            }

            return resultFrame;
        }
    }
}
