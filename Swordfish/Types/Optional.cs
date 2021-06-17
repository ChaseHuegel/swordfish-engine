namespace Swordfish
{
    //  Represents an optional field
    public struct Optional<T>
    {
        public bool Enabled;
        public T Value;

        public Optional(T value)
        {
            Enabled = true;
            Value = value;
        }
    }
}
