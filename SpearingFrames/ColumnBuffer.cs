using System.Buffers;

namespace Spearing.Data.Frames
{
    public interface IColumnBuffer : IDisposable
    {
        int Length { get; }
        Type ElementType { get; }
        object? GetValue(int index);

        IColumnBuffer Gather(ReadOnlySpan<int> indices);
    }

    public class ColumnBuffer<T> : IColumnBuffer, IDisposable
    {
        private T[] _array;
        public int Length { get; }
        public Type ElementType => typeof(T);

        public Span<T> Span => _array.AsSpan(0, Length);

        public ColumnBuffer(int length)
        {
            Length = length;
            _array = ArrayPool<T>.Shared.Rent(length);
        }

        public ColumnBuffer(IEnumerable<T> source)
        {
            var data = source is T[] arr ? arr : source.ToArray();
            Length = data.Length;
            _array = ArrayPool<T>.Shared.Rent(Length);
            Array.Copy(data, _array, Length);
        }

        public T this[int i] { get => _array[i]; set => _array[i] = value; }
        public object? GetValue(int index) => _array[index];

        public IColumnBuffer Gather(ReadOnlySpan<int> indices)
        {
            // Rent new memory for the filtered result
            var result = new ColumnBuffer<T>(indices.Length);
            var sourceSpan = this.Span;
            var destSpan = result.Span;

            // The High-Speed Gather Loop
            for (int i = 0; i < indices.Length; i++)
            {
                destSpan[i] = sourceSpan[indices[i]];
            }

            return result;
        }

        public void Dispose() => ArrayPool<T>.Shared.Return(_array, typeof(T) != typeof(string));
    }
}
