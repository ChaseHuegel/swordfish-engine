namespace Swordfish.ECS;

public readonly ref struct Ref<T> where T : struct, IDataComponent
{
    private readonly ref T _value;
    private readonly ref byte _flag;

    public Ref(ref T value, ref byte flag)
    {
        _value = ref value;
        _flag = ref flag;
    }

    public ref readonly T Read => ref _value;

    public ref T Write
    {
        get
        {
            _flag |= DataFlags.DIRTY;
            return ref _value;
        }
    }
}
