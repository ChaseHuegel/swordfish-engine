using Swordfish.Library.Types;

namespace Swordfish.ECS;

public interface IECSContext
{
    DataBinding<double> Delta { get; }

    int MaxEntities { get; }

    EntityBuilder EntityBuilder { get; }

    void Start();
    void Stop();
    void Reset();
    void Update(float deltaTime);

    int BindComponent<TComponent>();
    TSystem BindSystem<TSystem>() where TSystem : ComponentSystem;

    int GetComponentIndex(Type type);
    int GetComponentIndex<TComponent>();

    Entity CreateEntity();
    Entity CreateEntity(object?[] components);

    void RemoveEntity(Entity entity);

    Entity[] GetEntities();
    Entity[] GetEntities(params Type[] componentTypes);
}