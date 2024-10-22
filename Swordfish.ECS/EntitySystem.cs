namespace Swordfish.ECS;

public interface IEntitySystem
{
    void Tick(float delta, DataStore store);
}

public abstract class EntitySystem<T1> : IEntitySystem
    where T1 : struct, IDataComponent
{
    public void Tick(float delta, DataStore store)
    {
        store.Query<T1>(delta, OnTick);
    }

    protected abstract void OnTick(float delta, DataStore store, int entity, ref T1 component1);
}

public abstract class EntitySystem<T1, T2> : IEntitySystem
    where T1 : struct, IDataComponent
    where T2 : struct, IDataComponent
{
    public void Tick(float delta, DataStore store)
    {
        store.Query<T1, T2>(delta, OnTick);
    }

    protected abstract void OnTick(float delta, DataStore store, int entity, ref T1 component1, ref T2 component2);
}