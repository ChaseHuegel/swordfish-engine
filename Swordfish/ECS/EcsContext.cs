using System.Collections;

namespace Swordfish.ECS
{
    public class EcsContext
    {
        public ComponentSystems systems;
        public ExpandingList<Entity> entities;
        public Queue recycledIDs;

        public EcsContext()
        {
            //  Components must be registered first
            Component.RegisterComponents();
            ComponentSystems.RegisterSystems();

            systems = new ComponentSystems(this);
            entities = new ExpandingList<Entity>();
            recycledIDs = new Queue();
        }

        public void Start()
        {
            systems.Start();
        }

        public void Step()
        {
            systems.Update();
        }

        public void Shutdown()
        {
            systems.Destroy();
        }

        private ushort CreateEntityID()
        {
            if (recycledIDs.Count > 0)
                return (ushort)recycledIDs.Dequeue();

            return (ushort)entities.Count;
        }

        public Entity CreateEntity()
        {
            entities.Add(
                new Entity(CreateEntityID(), this)
            );

            return entities[entities.Count];
        }

        public void PushEntity(Entity entity)
        {
            entity.ID = CreateEntityID();
            entity.Context = this;

            entities.Add(entity);
        }

        public void DestroyEntity(Entity entity)
        {
            entities[entity.ID] = null;
            recycledIDs.Enqueue(entity.ID);
        }
    }
}
