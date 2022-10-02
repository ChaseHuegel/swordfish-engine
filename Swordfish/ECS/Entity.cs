using System.Collections;
using System.Runtime.CompilerServices;
using Swordfish.Library.Types;

namespace Swordfish.ECS;

public struct Entity
{
    public const int Null = ChunkedDataStore.NullPtr;

    public int Ptr { get; }
    public ECSContext World { get; }

    internal Entity(int ptr, ECSContext world)
    {
        Ptr = ptr;
        World = world;
    }

    public bool AddComponent<TComponent>() where TComponent : class, new()
        => AddComponent<TComponent>(World.GetComponentIndex<TComponent>());

    public void SetComponent<TComponent>(TComponent component) where TComponent : class
        => SetComponent(World.GetComponentIndex<TComponent>(), component);

    public TComponent? GetComponent<TComponent>() where TComponent : class
        => GetComponent<TComponent>(World.GetComponentIndex<TComponent>());

    public bool TryGetComponent<TComponent>(out TComponent? component) where TComponent : class
        => TryGetComponent(World.GetComponentIndex<TComponent>(), out component);

    public bool HasComponent<TComponent>() where TComponent : class
        => HasComponent(World.GetComponentIndex<TComponent>());

    public bool AddComponent<TComponent>(int componentIndex) where TComponent : class, new()
    {
        if (HasComponent(componentIndex))
            return false;

        World.Store.Set(Ptr, componentIndex, new TComponent());
        return true;
    }

    public void SetComponent<TComponent>(int componentIndex, TComponent component) where TComponent : class
    {
        World.Store.Set(Ptr, componentIndex, component);
    }

    public TComponent? GetComponent<TComponent>(int componentIndex) where TComponent : class
    {
        return World.Store.GetAt(Ptr, componentIndex) as TComponent;
    }

    public bool TryGetComponent<TComponent>(int componentIndex, out TComponent? component) where TComponent : class
    {
        return (component = GetComponent<TComponent>(componentIndex)) != null;
    }

    public bool HasComponent(int componentIndex)
    {
        return World.Store.HasAt(Ptr, componentIndex);
    }

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
