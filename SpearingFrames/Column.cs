namespace Spearing.Data.Frames
{
    public class Column
    {
        public string Name { get; set; }
        //public string Name { get; internal set; }
        public IColumnBuffer Buffer { get; }

        public Column(IColumnBuffer buffer) => Buffer = buffer;
        //public Column(IColumnBuffer buffer, string name) { Buffer = buffer; Name = name; }

        public ColumnEnumerable<T> As<T>()
        {
            if (Buffer is ColumnBuffer<T> typed) return new ColumnEnumerable<T>(typed);
            throw new InvalidCastException($"Buffer is {Buffer.ElementType.Name}, not {typeof(T).Name}");
        }

        // --- 1. Implicit Operators (Lists: All Primitives + Nullables) ---
        public static implicit operator Column(List<bool> c) => new(c.ToColumn());
        public static implicit operator Column(List<bool?> c) => new(c.ToColumn());
        public static implicit operator Column(List<byte> c) => new(c.ToColumn());
        public static implicit operator Column(List<byte?> c) => new(c.ToColumn());
        public static implicit operator Column(List<sbyte> c) => new(c.ToColumn());
        public static implicit operator Column(List<sbyte?> c) => new(c.ToColumn());
        public static implicit operator Column(List<char> c) => new(c.ToColumn());
        public static implicit operator Column(List<char?> c) => new(c.ToColumn());
        public static implicit operator Column(List<short> c) => new(c.ToColumn());
        public static implicit operator Column(List<short?> c) => new(c.ToColumn());
        public static implicit operator Column(List<ushort> c) => new(c.ToColumn());
        public static implicit operator Column(List<ushort?> c) => new(c.ToColumn());
        public static implicit operator Column(List<int> c) => new(c.ToColumn());
        public static implicit operator Column(List<int?> c) => new(c.ToColumn());
        public static implicit operator Column(List<uint> c) => new(c.ToColumn());
        public static implicit operator Column(List<uint?> c) => new(c.ToColumn());
        public static implicit operator Column(List<long> c) => new(c.ToColumn());
        public static implicit operator Column(List<long?> c) => new(c.ToColumn());
        public static implicit operator Column(List<ulong> c) => new(c.ToColumn());
        public static implicit operator Column(List<ulong?> c) => new(c.ToColumn());
        public static implicit operator Column(List<float> c) => new(c.ToColumn());
        public static implicit operator Column(List<float?> c) => new(c.ToColumn());
        public static implicit operator Column(List<double> c) => new(c.ToColumn());
        public static implicit operator Column(List<double?> c) => new(c.ToColumn());
        public static implicit operator Column(List<decimal> c) => new(c.ToColumn());
        public static implicit operator Column(List<decimal?> c) => new(c.ToColumn());
        public static implicit operator Column(List<DateTime> c) => new(c.ToColumn());
        public static implicit operator Column(List<DateTime?> c) => new(c.ToColumn());
        public static implicit operator Column(List<TimeSpan> c) => new(c.ToColumn());
        public static implicit operator Column(List<TimeSpan?> c) => new(c.ToColumn());
        public static implicit operator Column(List<Guid> c) => new(c.ToColumn());
        public static implicit operator Column(List<Guid?> c) => new(c.ToColumn());
        public static implicit operator Column(List<string> c) => new(c.ToColumn());
        public static implicit operator Column(List<object> c) => new(c.ToColumn());

        // --- 2. Implicit Operators (Arrays: Mirroring Lists Exactly) ---
        public static implicit operator Column(bool[] c) => new(c.ToColumn());
        public static implicit operator Column(bool?[] c) => new(c.ToColumn());
        public static implicit operator Column(byte[] c) => new(c.ToColumn());
        public static implicit operator Column(byte?[] c) => new(c.ToColumn());
        public static implicit operator Column(sbyte[] c) => new(c.ToColumn());
        public static implicit operator Column(sbyte?[] c) => new(c.ToColumn());
        public static implicit operator Column(char[] c) => new(c.ToColumn());
        public static implicit operator Column(char?[] c) => new(c.ToColumn());
        public static implicit operator Column(short[] c) => new(c.ToColumn());
        public static implicit operator Column(short?[] c) => new(c.ToColumn());
        public static implicit operator Column(ushort[] c) => new(c.ToColumn());
        public static implicit operator Column(ushort?[] c) => new(c.ToColumn());
        public static implicit operator Column(int[] c) => new(c.ToColumn());
        public static implicit operator Column(int?[] c) => new(c.ToColumn());
        public static implicit operator Column(uint[] c) => new(c.ToColumn());
        public static implicit operator Column(uint?[] c) => new(c.ToColumn());
        public static implicit operator Column(long[] c) => new(c.ToColumn());
        public static implicit operator Column(long?[] c) => new(c.ToColumn());
        public static implicit operator Column(ulong[] c) => new(c.ToColumn());
        public static implicit operator Column(ulong?[] c) => new(c.ToColumn());
        public static implicit operator Column(float[] c) => new(c.ToColumn());
        public static implicit operator Column(float?[] c) => new(c.ToColumn());
        public static implicit operator Column(double[] c) => new(c.ToColumn());
        public static implicit operator Column(double?[] c) => new(c.ToColumn());
        public static implicit operator Column(decimal[] c) => new(c.ToColumn());
        public static implicit operator Column(decimal?[] c) => new(c.ToColumn());
        public static implicit operator Column(DateTime[] c) => new(c.ToColumn());
        public static implicit operator Column(DateTime?[] c) => new(c.ToColumn());
        public static implicit operator Column(TimeSpan[] c) => new(c.ToColumn());
        public static implicit operator Column(TimeSpan?[] c) => new(c.ToColumn());
        public static implicit operator Column(Guid[] c) => new(c.ToColumn());
        public static implicit operator Column(Guid?[] c) => new(c.ToColumn());
        public static implicit operator Column(string[] c) => new(c.ToColumn());
        public static implicit operator Column(object[] c) => new(c.ToColumn());

        // --- 3. Math Buffer Assignments (Raw ColumnBuffers) ---
        public static implicit operator Column(ColumnBuffer<bool> b) => new(b);
        public static implicit operator Column(ColumnBuffer<int> b) => new(b);
        public static implicit operator Column(ColumnBuffer<long> b) => new(b);
        public static implicit operator Column(ColumnBuffer<float> b) => new(b);
        public static implicit operator Column(ColumnBuffer<double> b) => new(b);
        public static implicit operator Column(ColumnBuffer<decimal> b) => new(b);
        public static implicit operator Column(ColumnBuffer<string> b) => new(b);
        public static implicit operator Column(ColumnBuffer<DateTime> b) => new(b);

        // --- 4. Fluent Bridge (Math Operation Results) ---
        public static implicit operator Column(ColumnEnumerable<bool> ce) => new(ce._buffer);
        public static implicit operator Column(ColumnEnumerable<int> ce) => new(ce._buffer);
        public static implicit operator Column(ColumnEnumerable<long> ce) => new(ce._buffer);
        public static implicit operator Column(ColumnEnumerable<float> ce) => new(ce._buffer);
        public static implicit operator Column(ColumnEnumerable<double> ce) => new(ce._buffer);
        public static implicit operator Column(ColumnEnumerable<decimal> ce) => new(ce._buffer);
        public static implicit operator Column(ColumnEnumerable<string> ce) => new(ce._buffer);
        public static implicit operator Column(ColumnEnumerable<DateTime> ce) => new(ce._buffer);


        // Other
        //public static implicit operator Column(IColumnBuffer buffer) => new Column(buffer);
    }
}
