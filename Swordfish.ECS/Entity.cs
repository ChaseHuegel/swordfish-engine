namespace Swordfish.ECS;

public readonly struct Entity
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

    // ReSharper disable once UnusedMember.Global
    public bool Add<T1>() where T1 : struct, IDataComponent
    {
        if (Has<T1>())
        {
            return false;
        }

        _dataStore.AddOrUpdate(Ptr, default(T1));
        return true;
    }

    public void AddOrUpdate<T1>(T1 component) where T1 : struct, IDataComponent
    {
        _dataStore.AddOrUpdate(Ptr, component);
    }
    
    public bool Remove<T1>() where T1 : struct, IDataComponent
    {
        return _dataStore.Remove<T1>(Ptr);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public bool Has<T1>() where T1 : struct, IDataComponent
    {
        return _dataStore.TryGet<T1>(Ptr, out _);
    }

    // ReSharper disable once UnusedMember.Global
    public bool TryGet<T1>(out T1 component1) where T1 : struct, IDataComponent
    {
        return _dataStore.TryGet(Ptr, out component1);
    }

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
