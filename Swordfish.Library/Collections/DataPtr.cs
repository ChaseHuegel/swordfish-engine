namespace Swordfish.Library.Collections
{
    public struct DataPtr<T>
    {
        public int Ptr;
        public T Data;

        public DataPtr(int ptr, T data)
        {
            Ptr = ptr;
            Data = data;
        }
    }
}