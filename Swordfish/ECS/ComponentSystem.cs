namespace Swordfish.ECS;

public abstract class ComponentSystem
{
    internal bool Modified { get; set; } = true;

    protected Entity[] Entities;

    internal Type[] Filter;

    protected abstract void Update(Entity entity, float deltaTime);

    protected virtual void OnModified() { }

    protected virtual void OnUpdated() { }
    protected virtual void Update(float deltaTime) { }

    public ComponentSystem()
    {
        Filter = Array.Empty<Type>();
        Entities = Array.Empty<Entity>();
    }

    public ComponentSystem(Type[] filter) : this()
    {
        Filter = filter ?? Array.Empty<Type>();
        Entities = Array.Empty<Entity>();
    }

    public void Update(ECSContext world, float deltaTime)
    {
        if (Modified)
        {
            Entities = Filter.Length > 0 ? world.GetEntities(Filter) : world.GetEntities();
            //  ! TODO This is because of some kind of race condition. This needs to be resolved because 0 entities is a valid modified state.
            if (Entities.Length > 0)
            {
                Modified = false;
                OnModified();
            }
        }

        for (int i = 0; i < Entities.Length; i++)
            Update(Entities[i], deltaTime);

        Update(deltaTime);
        OnUpdated();
    }

    /// <summary>
    ///     Check if the system has a specified type in its filter.
    /// </summary>
    internal bool IsFiltering<T>() => IsFiltering(typeof(T));

    /// <summary>
    ///     Check if the system has a specified type in its filter.
    /// </summary>
    internal bool IsFiltering(Type type)
    {
        foreach (Type t in Filter)
            if (t == type) return true;

        return false;
    }

    /// <summary>
    ///     Check if the system has any of the specified types in its filter.
    /// </summary>
    internal bool IsFilteringAny(params Type[] types)
    {
        foreach (Type t in Filter)
            foreach (Type t2 in types)
                if (t == t2) return true;

        return false;
    }

    /// <summary>
    ///     Check if the system has all of the specified types in its filter.
    /// </summary>
    internal bool IsFilteringAll(params Type[] types)
    {
        int matches = 0;
        foreach (Type t in Filter)
            foreach (Type t2 in types)
                if (t == t2) matches++;

        return matches == types.Length;
    }
}
