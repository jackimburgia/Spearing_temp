using Spearing.Data.Frames;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BridgeTester1.NewStructure2
{

    public static class FrameQueryEngine
    {
        public static (Column column, SqlQueryStatement query) ExecuteScalarAll(this Dictionary<string, Frame> db, string sql)
        {
            // 1. Instantiate the engine (since Execute is not static)
            //var engine = new FrameQueryEngine(db);

            // 2. Run the query
            (Frame resultFrame, var query) = db.GetFrameAll(sql);

            // 3. Get the first column name using the new public property
            string firstColName = resultFrame.ColumnNames.FirstOrDefault();
            if (firstColName == null) 
                return (null, null);

            // 4. Use the indexer to get the result column
            Column resultCol = resultFrame[firstColName];
            IColumnBuffer resultBuffer = resultCol.Buffer;

            // 5. Extract the scalar value (e.g., the SUM result)
            object scalarValue = resultBuffer.Length > 0 ? resultBuffer.GetValue(0) : null;
            Type elementType = resultBuffer.ElementType;

            // 6. Nullable Promotion Logic (Critical for "decimal?" support)
            Type storageType = elementType;
            if (scalarValue == null && elementType.IsValueType && Nullable.GetUnderlyingType(elementType) == null)
            {
                // Converts 'decimal' to 'decimal?' so it can hold the 'null'
                storageType = typeof(Nullable<>).MakeGenericType(elementType);
            }

            // TODO - improve this
            int targetRowCount = 1;

            // 7. Broadcast the value
            // We create an array of targetRowCount (e.g., 1 for your 'deal' frame)
            Array data = Array.CreateInstance(storageType, targetRowCount);



            for (int i = 0; i < targetRowCount; i++)
            {
                data.SetValue(scalarValue, i);
            }

            // TODO - improve this
            var result = data.OfType<object>().ToArray();
            var t = result.InferFullType();
            var result2 = result.CreateTypedList(t);

            var col = new Column(result2.ToColumn());
            return (col, query);


            //var col = result.CreateTypedList()

            // 8. Return the new Column
            // Your Frame indexer setter will handle setting the .Name property
            //return new Column(data.ToColumn());
        }
        // TODO - redo this
        public static Column ExecuteScalar(this Dictionary<string, Frame> db, string sql)
        {
            (Column column, _) = db.ExecuteScalarAll(sql);
            return column;
        }

        public static (Frame frame, SqlQueryStatement query) GetFrameAll(this Dictionary<string, Frame> _context, string sql)
        {
            SqlParser parser = new SqlParser(sql);
            SqlQueryStatement query = parser.ParseQuery();

            if (!_context.TryGetValue(query.From, out Frame? sourceFrame))
                throw new Exception($"Frame '{query.From}' not found.");

            IEnumerable<Row> rows = sourceFrame;

            // 1. WHERE
            if (query.Where != null)
            {
                var predicate = DataFrameEvaluator.CompileFormula(query.Where.Expression, sourceFrame);
                rows = rows.Where(r => (bool)predicate(r));
            }

            // 2. ORDER BY
            if (query.OrderBy != null && query.OrderBy.Terms.Any())
            {
                rows = ApplySorting(rows, query.OrderBy, sourceFrame);
            }

            // 3. Determine Path: Aggregate vs Standard
            bool isAggregateQuery = query.Select.Projections.Any(p => p.Expression is AggregateExpr);

            if (isAggregateQuery)
            {
                var aggFrame = ExecuteAggregateQuery(rows, query, sourceFrame);
                return (aggFrame, query);
            }

            var frame = ProjectStandard(rows, query.Select, sourceFrame);

            return (frame, query);
        }

        public static Frame GetFrame(this Dictionary<string, Frame> _context, string sql)
        {
            (Frame frame, _) = _context.GetFrameAll(sql);

            return frame;
        }

        private static Frame ProjectStandard(IEnumerable<Row> rows, SelectStatement select, Frame source)
        {
            var rowList = rows.ToList();
            var newFrame = new Frame(rowList.Count);

            foreach (var projection in select.Projections)
            {
                var func = DataFrameEvaluator.CompileFormula(projection.Expression, source);
                var columnData = rowList.Select(r => func(r)).ToArray();

                string colName = projection.Alias ?? GetDefaultName(projection.Expression);
                newFrame.AddColumn(colName, columnData);
            }

            return newFrame;
        }

        private static Frame ExecuteAggregateQuery(IEnumerable<Row> rows, SqlQueryStatement query, Frame source)
        {
            // Implicit Grouping: Non-aggregates are keys, aggregates are values
            var keyProjections = query.Select.Projections.Where(p => !(p.Expression is AggregateExpr)).ToList();
            var aggProjections = query.Select.Projections.Where(p => p.Expression is AggregateExpr).ToList();

            var keySelectors = keyProjections.Select(p => new {
                Name = p.Alias ?? GetDefaultName(p.Expression),
                Func = DataFrameEvaluator.CompileFormula(p.Expression, source)
            }).ToList();

            // TODO - add a safety check for ths Arguments[0]
            var aggSelectors = aggProjections
                .Select(p => new {
                    Name = p.Alias ?? "Agg",
                    Expr = (AggregateExpr)p.Expression,
                    //InnerFunc = DataFrameEvaluator.CompileFormula(((AggregateExpr)p.Expression).Argument, source)
                    InnerFunc = DataFrameEvaluator.CompileFormula(((AggregateExpr)p.Expression).Arguments[0], source)
                })
                .ToList();

            // Grouping logic
            var groupedRows = rows.GroupBy(r => {
                if (keySelectors.Count == 0) return "GLOBAL";
                return string.Join("|", keySelectors.Select(ks => ks.Func(r)?.ToString() ?? "null"));
            });

            var resultRows = new List<object[]>();

            foreach (var group in groupedRows)
            {
                var rowValues = new object[keySelectors.Count + aggSelectors.Count];
                int colIdx = 0;

                // Keys
                var firstInGroup = group.First();
                foreach (var ks in keySelectors) 
                { 
                    var value = ks.Func(firstInGroup);
                    var t1 = value.GetType();
                    rowValues[colIdx++] = value;
                }

                // Values (Aggregates)
                foreach (var agg in aggSelectors)
                {
                    var aggValue = CalculateAggregate(group, agg.Expr, agg.InnerFunc);
                    var t1 = aggValue.GetType();
                    rowValues[colIdx++] = aggValue;
                }

                resultRows.Add(rowValues);
            }

            var newFrame = new Frame(resultRows.Count);
            var names = keySelectors
                .Select(k => k.Name)
                .Concat(aggSelectors.Select(a => a.Name))
                .ToList();

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                int idx = i;
                var values = resultRows.Select(r => r[idx]).ToArray();

                // TODO - figure this out
                // values is typed as object; needs to be added as correct type

                var t = values.InferFullType();
                var typedList = values.CreateTypedList(t);

                newFrame.AddColumn(name, typedList);


                //newFrame.AddColumn(name, values);
            }

            return newFrame;
        }

        //public static IList CreateTypedList(IEnumerable<object> items, Type type)
        //{
        //    // Create a List<T> where T is your inferred type
        //    var listType = typeof(List<>).MakeGenericType(type);
        //    var list = (IList)Activator.CreateInstance(listType);

        //    foreach (var item in items)
        //    {
        //        // Convert.ChangeType handles minor mismatches (like int to decimal)
        //        list.Add(Convert.ChangeType(item, type));
        //    }
        //    return list;
        //}


        private static object CalculateAggregate(IEnumerable<Row> group, AggregateExpr agg, Func<Row, object> innerFunc)
        {
            var values = group.Select(r => innerFunc(r)).Where(v => v != null).ToList();

            if (agg.Func == AggregateFunc.Count) return values.Count;
            if (values.Count == 0) return null;

            Type type = values[0].GetType();

            if (agg.Func == AggregateFunc.Max || agg.Func == AggregateFunc.Min)
            {
                var comp = values.Cast<IComparable>();
                return agg.Func == AggregateFunc.Max ? comp.Max() : comp.Min();
            }

            // Strict Type Matching Math
            if (type == typeof(decimal)) return ExecuteAgg<decimal>(values, agg.Func);
            if (type == typeof(double)) return ExecuteAgg<double>(values, agg.Func);
            if (type == typeof(int)) return ExecuteAgg<int>(values, agg.Func);
            if (type == typeof(long)) return ExecuteAgg<long>(values, agg.Func);
            if (type == typeof(float)) return ExecuteAgg<float>(values, agg.Func);

            // Fallback for smaller integers
            return ExecuteAgg<decimal>(values.Select(v => Convert.ToDecimal(v)).Cast<object>().ToList(), agg.Func);
        }

        private static object ExecuteAgg<T>(List<object> values, AggregateFunc func) where T : struct
        {
            var typed = values.Cast<T>().ToList();
            dynamic sum = default(T);
            foreach (var v in typed) sum += v;

            if (func == AggregateFunc.Sum) return (T)sum;

            if (func == AggregateFunc.Avg)
            {
                if (typeof(T) == typeof(decimal)) return (decimal)sum / typed.Count;
                return Convert.ToDouble(sum) / typed.Count;
            }
            return null;
        }

        private static IEnumerable<Row> ApplySorting(IEnumerable<Row> rows, OrderByStatement orderBy, Frame source)
        {
            IOrderedEnumerable<Row>? ordered = null;
            for (int i = 0; i < orderBy.Terms.Count; i++)
            {
                var term = orderBy.Terms[i];
                var selector = DataFrameEvaluator.CompileFormula(term.Expression, source);
                if (i == 0)
                    ordered = term.IsAscending ? rows.OrderBy(r => selector(r)) : rows.OrderByDescending(r => selector(r));
                else
                    ordered = term.IsAscending ? ordered!.ThenBy(r => selector(r)) : ordered!.ThenByDescending(r => selector(r));
            }
            return ordered ?? rows;
        }

        private static string GetDefaultName(IExpression expr) => expr is IdentifierExpr e ? e.Name : "Column";
    }
}
