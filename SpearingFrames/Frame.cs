using System.Buffers;
using System.Collections;

namespace Spearing.Data.Frames
{
    //internal delegate ColumnBuffer<T> ArrayOp<T>(ReadOnlySpan<T> a, T[] b);


    //internal record ColumnEnumerableInternal(IColumnBuffer _buffer);

    public class Frame : IEnumerable<Row>, IDisposable
    {
        internal readonly Dictionary<string, IColumnBuffer> _columns = new(StringComparer.OrdinalIgnoreCase);
        public Column this[string columnName]
        {
            get
            {
                if (_columns.TryGetValue(columnName, out var buffer))
                    return new Column(buffer);

                return null;
            }
            set
            {
                // The 'value' here is now a Column object thanks to the implicit operators above
                if (value?.Buffer != null)
                {
                    value.Name = columnName;
                    _columns[columnName] = value.Buffer;

                    // Keep the frame row count in sync
                    if (value.Buffer.Length > RowCount)
                        RowCount = value.Buffer.Length;
                }
            }
        }

        public void AddColumn(string name, IEnumerable data)
        {
            var type = GetElementType(data);
            //var method = typeof(Frame).GetMethod("CreateTypedBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            
            var method = typeof(Frame)
                .GetMethod(nameof(Frame.CreateTypedBuffer), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(type);

            method.Invoke(this, [name, data]);
        }

        //public void AddColumn(string name, IColumnBuffer buffer)
        //{
        //    // The dictionary wants the buffer (the data), not the Column (the wrapper)
        //    _columns[name] = buffer;
        //}

        private void CreateTypedBuffer<T>(string name, IEnumerable<T> data)
        {
            T[] arr = data as T[] ?? data.ToArray();

            if (_columns.Count == 0) 
                RowCount = arr.Length;
            else if (arr.Length != RowCount) 
                throw new Exception("Length mismatch.");

            var buffer = new ColumnBuffer<T>(RowCount);
            arr.AsSpan().CopyTo(buffer.Span);

            _columns[name] = buffer;
        }






        public int RowCount { get; private set; }
        public IEnumerable<string> ColumnNames => _columns.Keys;

        public Frame() { }
        public Frame(int rowCount) => RowCount = rowCount;

        public Row this[int index] => new Row(this, index);


        //public Column this[string name]
        //{
        //    get => _columns.TryGetValue(name, out var b) ? new Column(name, b) : throw new KeyNotFoundException(name);
        //    set
        //    {
        //        object val = value;
        //        if (val is IEnumerable data and not string)
        //        {
        //            AddColumn(name, data);
        //        }
        //        else if (val is Column col)
        //        {
        //            if (this.RowCount > 0 && col._buffer.Length != this.RowCount)
        //                throw new Exception($"Row counts don't match: {this.RowCount}, {col._buffer.Length}");

        //            col.Name = name;
        //            _columns[name] = col._buffer;
        //            if (_columns.Count == 1) RowCount = col._buffer.Length;
        //        }
        //        else if (val is IColumnBuffer buffer)
        //        {
        //            if (this.RowCount > 0 && buffer.Length != this.RowCount)
        //                throw new Exception("Row counts don't match.");
        //            _columns[name] = buffer;
        //        }
        //    }
        //}



        private Type GetElementType(IEnumerable source) =>
            source.GetType().IsArray ? source.GetType().GetElementType()! :
            source.GetType().GetGenericArguments().FirstOrDefault() ?? typeof(object);

        internal ColumnBuffer<T> GetTypedBuffer<T>(string name) =>
            _columns[name] as ColumnBuffer<T> ?? throw new InvalidCastException();

        public IEnumerator<Row> GetEnumerator() { for (int i = 0; i < RowCount; i++) yield return new Row(this, i); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { foreach (var c in _columns.Values) c.Dispose(); }
    }
}
