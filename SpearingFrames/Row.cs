namespace Spearing.Data.Frames
{
    public class Row
    {
        private readonly Frame _parent;
        internal int _index;
        internal Row(Frame parent, int index) { _parent = parent; _index = index; }
        public T Get<T>(string col) => _parent.GetTypedBuffer<T>(col)[_index];
        internal Frame GetParentInternal() => _parent;
    }
}
