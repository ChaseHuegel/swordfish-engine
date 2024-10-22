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

    public bool AddComponent<T1>() where T1 : struct, IDataComponent
    {
        if (HasComponent<T1>())
            return false;

        DataStore.AddOrUpdate(Ptr, default(T1));
        return true;
    }

    public void SetComponent<T1>(T1 component) where T1 : struct, IDataComponent
    {
        DataStore.AddOrUpdate(Ptr, component);
    }

    public bool HasComponent<T1>() where T1 : struct, IDataComponent
    {
        return DataStore.TryGet<T1>(Ptr, out _);
    }

    public bool TryGetComponent<T1>(out T1 component1) where T1 : struct, IDataComponent
    {
        return DataStore.TryGet(Ptr, out component1);
    }

    public T1? GetComponent<T1>() where T1 : struct, IDataComponent
    {
        if (DataStore.TryGet(Ptr, out T1 component1))
        {
            return component1;
        }

        return null;
    }

    public Span<IDataComponent> GetComponents()
    {
        return DataStore.Get(Ptr);
    }
}
