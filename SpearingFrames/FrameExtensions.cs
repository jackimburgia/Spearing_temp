using System.Buffers;
using System.Collections;
using System.Formats.Asn1;
using System.Reflection;
using System.Text;

namespace Spearing.Data.Frames
{


    public static class FrameExtensions
    {
        public static Column Map(this Frame sourceFrame,
                                 string sourceColumnName,
                                 Frame lookupFrame,
                                 string lookupKeyColumnName,
                                 string lookupMapColumnName,
                                 object defaultValue)
        {
            // 1. Get references to the source and lookup columns
            var sourceCol = sourceFrame[sourceColumnName];
            var lookupKeyCol = lookupFrame[lookupKeyColumnName];
            var lookupValCol = lookupFrame[lookupMapColumnName];

            // 2. Build the high-performance Lookup Map (O(N))
            // We use 'object' keys to allow mapping between ints, strings, Guids, etc.
            var mapping = new Dictionary<object, object>();
            for (int i = 0; i < lookupFrame.RowCount; i++)
            {
                var key = lookupKeyCol.Buffer.GetValue(i);
                var val = lookupValCol.Buffer.GetValue(i);

                // We only map the first instance found (standard VLOOKUP behavior)
                if (key != null && !mapping.ContainsKey(key))
                {
                    mapping[key] = val;
                }
            }

            // 3. Prepare the Result Buffer
            Type targetType = lookupValCol.Buffer.ElementType;
            var resultArray = Array.CreateInstance(targetType, sourceFrame.RowCount);

            // 4. Handle Default Value Type Conversion
            // We ensure the 'defaultValue' provided by the user matches the target column's type
            object? finalDefault = defaultValue;
            if (defaultValue != null && defaultValue.GetType() != targetType)
            {
                // This converts "Other" (string) to 0 if the target column is an int, etc.
                finalDefault = Convert.ChangeType(defaultValue, targetType);
            }

            // 5. Populate the result by probing the Map (O(1) per row)
            var sourceBuffer = sourceCol.Buffer;
            for (int i = 0; i < sourceFrame.RowCount; i++)
            {
                var keyToFind = sourceBuffer.GetValue(i);

                if (keyToFind != null && mapping.TryGetValue(keyToFind, out var matchedValue))
                {
                    resultArray.SetValue(matchedValue, i);
                }
                else
                {
                    resultArray.SetValue(finalDefault, i);
                }
            }

            // 6. Wrap in a Column and Return
            // We give it a temporary name; your Frame's indexer setter will likely re-label it
            IColumnBuffer resultBuffer = resultArray.ToColumn();
            return new Column(resultBuffer);//, $"{sourceColumnName}_Mapped");
        }


        public static IColumnBuffer ToColumn(this object source)
        {
            if (source == null) return null;
            if (source is IColumnBuffer buf) return buf;
            if (source is IColumnWrapper wrapper) return wrapper.Buffer;

            return source switch
            {
                // --- 1. Boolean & Character ---
                bool[] b => new ColumnBuffer<bool>(b),
                bool?[] nb => ToColumnNullable(nb),
                char[] c => new ColumnBuffer<char>(c),
                char?[] nc => ToColumnNullable(nc),

                // --- 2. Floating Point & Financial ---
                double[] d => new ColumnBuffer<double>(d),
                double?[] nd => ToColumnNullable(nd),
                decimal[] dc => new ColumnBuffer<decimal>(dc),
                decimal?[] ndc => ToColumnNullable(ndc),
                float[] f => new ColumnBuffer<float>(f),
                float?[] nf => ToColumnNullable(nf),

                // --- 3. Integers (Signed) ---
                sbyte[] sb => new ColumnBuffer<sbyte>(sb),
                sbyte?[] nsb => ToColumnNullable(nsb),
                short[] sh => new ColumnBuffer<short>(sh),
                short?[] nsh => ToColumnNullable(nsh),
                int[] i => new ColumnBuffer<int>(i),
                int?[] ni => ToColumnNullable(ni),
                long[] l => new ColumnBuffer<long>(l),
                long?[] nl => ToColumnNullable(nl),

                // --- 4. Integers (Unsigned) ---
                byte[] by => new ColumnBuffer<byte>(by),
                byte?[] nby => ToColumnNullable(nby),
                ushort[] ush => new ColumnBuffer<ushort>(ush),
                ushort?[] nush => ToColumnNullable(nush),
                uint[] ui => new ColumnBuffer<uint>(ui),
                uint?[] nui => ToColumnNullable(nui),
                ulong[] ul => new ColumnBuffer<ulong>(ul),
                ulong?[] nul => ToColumnNullable(nul),

                // --- 5. Date, Time, & Structs ---
                DateTime[] dt => new ColumnBuffer<DateTime>(dt),
                DateTime?[] ndt => ToColumnNullable(ndt),
                TimeSpan[] ts => new ColumnBuffer<TimeSpan>(ts),
                TimeSpan?[] nts => ToColumnNullable(nts),
                Guid[] g => new ColumnBuffer<Guid>(g),
                Guid?[] ng => ToColumnNullable(ng),

                // --- 6. Reference Types ---
                string[] s => new ColumnBuffer<string>(s),
                object[] obj => new ColumnBuffer<object>(obj),

                // --- Fallback ---
                IEnumerable en => ToColumnGeneric((dynamic)en),
                _ => throw new InvalidOperationException($"Type {source.GetType().Name} is not supported.")
            };
        }

        private static IColumnBuffer ToColumnNullable<T>(T?[] source) where T : struct
        {
            var len = source.Length;
            var buffer = new ColumnBuffer<T>(len);
            var span = buffer.Span;

            for (int i = 0; i < len; i++)
            {
                // If the value is null, we use 'default'. 
                // In a production system, you'd also call buffer.MarkNull(i) here.
                span[i] = source[i].HasValue ? source[i].Value : default;
            }
            return buffer;
        }

        private static IColumnBuffer ToColumnGeneric<T>(IEnumerable<T> source)
        {
            // Now calling Constructor 2 in ColumnBuffer<T>
            return new ColumnBuffer<T>(source);
        }

        public static ColumnBuffer<T> ToColumn<T>(this IEnumerable<T> data)
        {
            T[] arr = data as T[] ?? data.ToArray();
            var buffer = new ColumnBuffer<T>(arr.Length);
            arr.AsSpan().CopyTo(buffer.Span);
            return buffer;
        }

        public static Frame ToFrame(this IEnumerable<Row> rows)
        {
            var list = rows.ToList();
            if (list.Count == 0) return new Frame(0);

            // Get the source frame from the flyweight Row
            var firstRow = list[0];
            var src = firstRow.GetParentInternal();
            var f = new Frame(list.Count);

            foreach (var name in src.ColumnNames)
            {
                var sourceBuffer = src._columns[name];

                // Resolve the generic copy method for this specific column type
                //var method = typeof(FrameExtensions).GetMethod("CopyData",
                var method = typeof(FrameExtensions).GetMethod(nameof(FrameExtensions.CopyData),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(sourceBuffer.ElementType);

                // 1. Use 'new object[]' to avoid collection expression ambiguity
                // 2. Explicitly wrap the returned IColumnBuffer in a new Column object
                var resultBuffer = (IColumnBuffer)method.Invoke(null, new object[] { sourceBuffer, list })!;

                // This now matches the indexer's expected 'Column' type
                f[name] = new Column(resultBuffer);
                //f[name] = new Column(name, resultBuffer);
            }

            return f;
        }

        public static Frame ToFrame<T>(this IEnumerable<T> source)
        {
            var list = source.ToList();
            int rowCount = list.Count;
            var frame = new Frame(rowCount);

            if (rowCount == 0) return frame;

            // Get all public properties of the class (e.g., Name, Age, HighScore)
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                // Extract values for this specific property across all objects
                var propertyValues = Array.CreateInstance(prop.PropertyType, rowCount);

                for (int i = 0; i < rowCount; i++)
                {
                    propertyValues.SetValue(prop.GetValue(list[i]), i);
                }

                // Add as a high-performance column
                frame.AddColumn(prop.Name, propertyValues);
            }

            return frame;
        }

        public static void Print(this Frame frame, int maxRows = 20)
        {
            if (frame.RowCount == 0)
            {
                Console.WriteLine("Frame is empty.");
                return;
            }

            var columnNames = frame.ColumnNames.ToList();
            var widths = new Dictionary<string, int>();

            // 1. Calculate Column Widths (Header vs first few rows)
            foreach (var name in columnNames)
            {
                int maxWidth = name.Length;
                int rowsToCheck = Math.Min(frame.RowCount, maxRows);

                for (int i = 0; i < rowsToCheck; i++)
                {
                    var val = frame._columns[name].GetValue(i)?.ToString() ?? "null";
                    if (val.Length > maxWidth) maxWidth = val.Length;
                }
                widths[name] = maxWidth + 2; // Add padding
            }

            // 2. Print Header
            var sb = new StringBuilder();
            foreach (var name in columnNames)
            {
                sb.Append(name.PadRight(widths[name]));
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine(new string('-', sb.Length));

            // 3. Print Rows
            for (int i = 0; i < Math.Min(frame.RowCount, maxRows); i++)
            {
                sb.Clear();
                foreach (var name in columnNames)
                {
                    var val = frame._columns[name].GetValue(i)?.ToString() ?? "null";
                    sb.Append(val.PadRight(widths[name]));
                }
                Console.WriteLine(sb.ToString());
            }

            if (frame.RowCount > maxRows)
            {
                Console.WriteLine($"... {frame.RowCount - maxRows} more rows.");
            }
            Console.WriteLine();
        }

        public static void SaveCsv(this Frame frame, string path)
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            var columnNames = frame.ColumnNames.ToList();

            // 1. Write Header
            writer.WriteLine(string.Join(",", columnNames));

            // 2. Write Data Rows
            for (int i = 0; i < frame.RowCount; i++)
            {
                var line = new StringBuilder();
                for (int j = 0; j < columnNames.Count; j++)
                {
                    var buffer = frame._columns[columnNames[j]];
                    var value = buffer.GetValue(i)?.ToString() ?? "";

                    // Basic CSV Escaping: Wrap in quotes if it contains a comma
                    if (value.Contains(","))
                    {
                        line.Append($"\"{value}\"");
                    }
                    else
                    {
                        line.Append(value);
                    }

                    if (j < columnNames.Count - 1) line.Append(",");
                }
                writer.WriteLine(line.ToString());
            }
        }

        private static ColumnBuffer<T> CopyData<T>(ColumnBuffer<T> src, List<Row> rows)
        {
            var buf = new ColumnBuffer<T>(rows.Count);
            var targetSpan = buf.Span;
            var sourceSpan = src.Span;

            // High-speed index-based copy
            for (int i = 0; i < rows.Count; i++)
            {
                targetSpan[i] = sourceSpan[rows[i]._index];
            }
            return buf;
        }

        #region ReadCsv

        #endregion
    }
}
