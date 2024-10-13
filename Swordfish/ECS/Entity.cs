using System.Runtime.CompilerServices;
using Swordfish.Library.Collections;

namespace Swordfish.ECS;

public struct Entity
{
    public const int Null = ChunkedDataStore.NullPtr;

    public int Ptr { get; } = Null;
    public ECSContext World { get; }
    public ChunkedDataStore Store { get; }

    internal Entity(int ptr, ECSContext world)
    {
        Ptr = ptr;
        World = world;
        Store = world.Store;
    }

    public bool AddComponent<TComponent>() where TComponent : class, new()
        => AddComponent<TComponent>(World.GetComponentIndex<TComponent>());

    public void SetComponent<TComponent>(TComponent component) where TComponent : class
        => SetComponent(World.GetComponentIndex<TComponent>(), component);

    public TComponent? GetComponent<TComponent>() where TComponent : class
        => GetComponent<TComponent>(World.GetComponentIndex<TComponent>());

    public bool HasComponent<TComponent>() where TComponent : class
        => HasComponent(World.GetComponentIndex<TComponent>());

    public bool AddComponent<TComponent>(int componentIndex) where TComponent : class, new()
    {
        if (HasComponent(componentIndex))
            return false;

        World.Store.Set(Ptr, componentIndex, new TComponent());
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void SetComponent<TComponent>(int componentIndex, TComponent component) where TComponent : class
    {
        World.Store.Set(Ptr, componentIndex, component);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TComponent? GetComponent<TComponent>(int componentIndex) where TComponent : class
    {
        return World.Store.GetAt(Ptr, componentIndex) as TComponent;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool HasComponent(int componentIndex)
    {
        return World.Store.HasAt(Ptr, componentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public object?[] GetComponents()
    {
        return World.Store.Get(Ptr);
    }

    public void ForEachComponent(Action<object?> action)
    {
        World.Store.ForEach(Ptr, action);
    }

    public IEnumerator<object?> GetEnumerator()
    {
        yield return World.Store.EnumerateAt(Ptr);
    }
}
