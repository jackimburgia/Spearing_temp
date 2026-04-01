using System.Buffers;
using System.Collections;
using static System.Collections.Specialized.BitVector32;

namespace Spearing.Data.Frames
{

    public class ColumnEnumerable<T> : IEnumerable<T>
    {
        internal readonly ColumnBuffer<T> _buffer;
        internal ColumnEnumerable(ColumnBuffer<T> buffer) => _buffer = buffer;

        public IEnumerator<T> GetEnumerator()
        {
            // Direct indexer access to avoid preserving Span across yield boundary
            for (int i = 0; i < _buffer.Length; i++)
            {
                yield return _buffer[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public ReadOnlySpan<T> AsSpan() => _buffer.Span;
        public int Count => _buffer.Length;

        // --- Math Operators ---
        public static ColumnEnumerable<T> operator *(ColumnEnumerable<T> a, ColumnEnumerable<T> b)
            => new(MathProvider<T>.Multiply(a.AsSpan(), b.AsSpan()));

        public static ColumnEnumerable<T> operator +(ColumnEnumerable<T> a, ColumnEnumerable<T> b)
            => new(MathProvider<T>.Add(a.AsSpan(), b.AsSpan()));

        public static ColumnEnumerable<T> operator -(ColumnEnumerable<T> a, ColumnEnumerable<T> b)
            => new(MathProvider<T>.Subtract(a.AsSpan(), b.AsSpan()));

        public static ColumnEnumerable<T> operator *(ColumnEnumerable<T> a, T s)
            => new(MathProvider<T>.MultiplyScalar(a.AsSpan(), s));

        public static ColumnEnumerable<T> operator +(ColumnEnumerable<T> a, T s)
            => new(MathProvider<T>.AddScalar(a.AsSpan(), s));

        public static ColumnEnumerable<T> operator *(ColumnEnumerable<T> a, T[] b)
            => new(MathProvider<T>.MultiplyArray(a.AsSpan(), b));


        // This "Shadows" the LINQ Where method
        //public Selection Where(Func<T, bool> predicate)
        //{
        //    var span = _buffer.Span;
        //    int len = span.Length;

        //    // Rent an index buffer from the pool to avoid allocations
        //    int[] indices = ArrayPool<int>.Shared.Rent(len);
        //    int count = 0;

        //    try
        //    {
        //        // The "Hot Loop" - direct Span access
        //        for (int i = 0; i < len; i++)
        //        {
        //            if (predicate(span[i]))
        //            {
        //                indices[count++] = i;
        //            }
        //        }

        //        // Return a Selection object (we'll define this next)
        //        // It holds the matching row numbers
        //        return new Selection(indices, count);
        //    }
        //    finally
        //    {
        //        // Note: We don't return to pool here; 
        //        // the Selection object will do it when finished.
        //    }
        //}

    }

    //public class Selection
    //{
    //    private readonly int[] _indices;
    //    private readonly int _count;

    //    public Selection(int[] indices, int count)
    //    {
    //        _indices = indices;
    //        _count = count;
    //    }

    //    public int Count => _count;
    //    public ReadOnlySpan<int> Indices => _indices.AsSpan(0, _count);

    //    // This allows: frame.Where(r => r.A > 0.8).ToFrame();
    //    public Frame ToFrame(Frame source)
    //    {
    //        var newFrame = new Frame();
    //        foreach (var col in source.Columns)
    //        {
    //            // High-speed "Gather" operation using the indices
    //            newFrame.AddColumn(col.Name, col.Buffer.Gather(Indices));
    //        }
    //        return newFrame;
    //    }
    //}
}