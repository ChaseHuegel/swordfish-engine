using System.Collections.Generic;
using Swordfish.Containers;

namespace Swordfish.ECS
{
    public class ECSContext
    {
        private ExpandingList<Entity> _entities = new ExpandingList<Entity>();
        private ExpandingList<int> _components = new ExpandingList<int>();
        private ExpandingList<object> _data = new ExpandingList<object>();
        private List<ComponentSystem> _systems = new List<ComponentSystem>();

        private Queue<int> _recycledIDs = new Queue<int>();

        /// <summary>
        /// Creates a new or recycled UID
        /// </summary>
        /// <returns>the UID</returns>
        internal int CreateEntityUID()
        {
            if (_recycledIDs.Count > 0) return _recycledIDs.Dequeue();

            return _entities.Count;
        }

        /// <summary>
        /// Pushes an entity to the context, ignoring duplicates
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>true if the push was sucessful</returns>
        public bool Push(Entity entity) => _entities.TryAdd(entity);

        /// <summary>
        /// Creates and pushes an entity to the context
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <returns>entity builder</returns>
        public Entity CreateEntity(string name = "", string tag = "")
        {
            Entity entity = new Entity(this, name, tag);

            if (!Push(entity)) Debug.Log("Entity already present in context", "ECS", LogType.WARNING);

            return entity;
        }
    }
}
