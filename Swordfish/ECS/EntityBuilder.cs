using Swordfish.Library.Types;

namespace Swordfish.ECS;

public class EntityBuilder
{
    private readonly World World;
    private readonly ChunkedDataStore Store;

    private object?[] Components
    {
        get => _Components ??= new object?[Store.ChunkSize];
        set => _Components = value;
    }

    private object?[]? _Components;

    public EntityBuilder(World world, ChunkedDataStore store)
    {
        World = world;
        Store = store;
        Components = new object?[Store.ChunkSize];
    }

    public EntityBuilder Clear()
    {
        for (int i = 0; i < Components.Length; i++)
            Components[i] = null;

        return this;
    }

    public Entity Build()
    {
        Entity entity = new(Store.Add(Components), World);
        World.Modified = true;

        Clear();
        return entity;
    }

    public EntityBuilder Attach<TComponent>() where TComponent : class, new()
        => Attach<TComponent>(World.GetComponentIndex<TComponent>());

    public EntityBuilder Attach<TComponent>(int componentIndex) where TComponent : class, new()
    {
        Components[componentIndex] = new TComponent();
        return this;
    }
}
