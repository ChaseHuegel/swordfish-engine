namespace Swordfish.ECS;

public partial class World
{
    public Entity[] GetEntities()
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        int[] ptrs = Store.All();
        Entity[] entities = new Entity[ptrs.Length];
        for (int i = 0; i < entities.Length; i++)
            entities[i] = new Entity(ptrs[i], this);

        return entities;
    }

    public Entity[] GetEntities(params Type[] componentTypes)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        int[] ptrs = Store.All();
        Entity[] entities = new Entity[ptrs.Length];
        int entityCount = 0;

        for (int i = 0; i < ptrs.Length; i++)
        {
            for (int n = 0; n < componentTypes.Length; n++)
            {
                if (Store.HasAt(ptrs[i], GetComponentIndex(componentTypes[n])))
                {
                    entities[entityCount++] = new Entity(ptrs[i], this);
                    break;
                }
            }
        }

        Entity[] results = new Entity[entityCount];
        Array.Copy(entities, 0, results, 0, entityCount);
        return results;
    }

    public void RemoveEntity(Entity entity)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        Modified = true;
        Store.Remove(entity.Ptr);
    }

    public Entity CreateEntity()
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        Modified = true;
        return new Entity(Store.Add(), this);
    }

    public Entity CreateEntity(params object[] components)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        Dictionary<int, object?> componentSet = new();
        for (int i = 0; i < components.Length; i++)
            componentSet.Add(GetComponentIndex(components[i].GetType()), components[i]);

        Modified = true;
        return new Entity(Store.Add(componentSet), this);
    }

    public Entity CreateEntity(params Type[] componentTypes)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        Dictionary<int, object?> componentSet = new();
        for (int i = 0; i < componentTypes.Length; i++)
        {
            Type type = componentTypes[i];
            componentSet.Add(GetComponentIndex(type), Activator.CreateInstance(type));
        }

        Modified = true;
        return new Entity(Store.Add(componentSet), this);
    }

    public Entity CreateEntity<T1>() where T1 : new()
        => CreateEntity(new T1());

    public Entity CreateEntity<T1, T2>() where T1 : new() where T2 : new()
        => CreateEntity(new T1(), new T2());

    public Entity CreateEntity<T1, T2, T3>() where T1 : new() where T2 : new() where T3 : new()
        => CreateEntity(new T1(), new T3());

    public Entity CreateEntity<T1, T2, T3, T4>() where T1 : new() where T2 : new() where T3 : new() where T4 : new()
        => CreateEntity(new T1(), new T3(), new T4());

    public Entity CreateEntity<T1, T2, T3, T4, T5>() where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
        => CreateEntity(new T1(), new T3(), new T4(), new T5());

    public Entity CreateEntity<T1, T2, T3, T4, T5, T6>() where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
        => CreateEntity(new T1(), new T3(), new T4(), new T5(), new T6());
}
