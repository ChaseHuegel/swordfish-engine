namespace Swordfish.ECS;

public readonly partial struct Entity
{
    // ReSharper disable once MemberCanBePrivate.Global
    public const int Null = 0;

    public int Ptr { get; } = Null;

    private readonly DataStore _dataStore;

    public Entity(int ptr, DataStore dataStore)
    {
        Ptr = ptr;
        _dataStore = dataStore;
    }

    public static implicit operator int(Entity entity) => entity.Ptr;

    public Uuid Uuid => _dataStore.GetUuid(Ptr);

    public static implicit operator Uuid(Entity entity) => entity.Uuid;

    public T1? Get<T1>() where T1 : struct, IDataComponent
    {
        if (_dataStore.TryGet(Ptr, out T1 component1))
        {
            return component1;
        }

        return null;
    }

    public Span<IDataComponent> GetAllData()
    {
        return _dataStore.Get(Ptr);
    }

    // ReSharper disable once UnusedMember.Global
    public void Destroy()
    {
        _dataStore.Free(Ptr);
    }
}
