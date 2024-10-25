namespace Swordfish.ECS;

public struct Entity
{
    public const int Null = 0;

    public int Ptr { get; } = Null;
    public DataStore DataStore { get; }

    public Entity(int ptr, DataStore dataStore)
    {
        Ptr = ptr;
        DataStore = dataStore;
    }

    public static implicit operator int(Entity entity) => entity.Ptr;

    public bool Add<T1>() where T1 : struct, IDataComponent
    {
        if (Has<T1>())
            return false;

        DataStore.AddOrUpdate(Ptr, default(T1));
        return true;
    }

    public void AddOrUpdate<T1>(T1 component) where T1 : struct, IDataComponent
    {
        DataStore.AddOrUpdate(Ptr, component);
    }

    public bool Has<T1>() where T1 : struct, IDataComponent
    {
        return DataStore.TryGet<T1>(Ptr, out _);
    }

    public bool TryGet<T1>(out T1 component1) where T1 : struct, IDataComponent
    {
        return DataStore.TryGet(Ptr, out component1);
    }

    public T1? Get<T1>() where T1 : struct, IDataComponent
    {
        if (DataStore.TryGet(Ptr, out T1 component1))
        {
            return component1;
        }

        return null;
    }

    public Span<IDataComponent> GetAllData()
    {
        return DataStore.Get(Ptr);
    }

    public void Destroy()
    {
        DataStore.Free(Ptr);
    }
}
