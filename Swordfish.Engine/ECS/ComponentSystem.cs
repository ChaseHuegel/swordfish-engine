using System;

namespace Swordfish.Engine.ECS
{
    public class ComponentSystem
    {
        //  TODO use Bitmask64 to speed up filter checking
        //       This will require tracking component indicies and limit # of components to 64
        public Type[] filter { get; internal set; }

        public override int GetHashCode() => GetType().GetHashCode();
        public override bool Equals(System.Object obj)
        {
            ComponentSystem system = obj as ComponentSystem;

            if (system == null) return false;

            //  Systems are identified by their type
            return system.GetType() == this.GetType();
        }

        protected int[] entities = new int[0];
        internal void PullEntities()
        {
            entities = Swordfish.ECS.PullPtr(filter);
            OnPullEntities();
        }

        internal void Update(float deltaTime)
        {
            OnUpdate(deltaTime);

            foreach (int entity in entities)
                OnUpdateEntity(deltaTime, entity);
        }

        /// <summary>
        /// Check if the system has a type in its filter
        /// </summary>
        /// <param name="type"></param>
        /// <returns>true if type is filtered; otherwise false</returns>
        public bool IsFiltering(Type type)
        {
            foreach (Type t in filter)
                if (t == type) return true;

            return false;
        }

        /// <summary>
        /// Check if the system has any types in its filter
        /// </summary>
        /// <param name="type"></param>
        /// <returns>true if type is filtered; otherwise false</returns>
        public bool IsFiltering(Type[] types)
        {
            foreach (Type t in filter)
                foreach (Type t2 in types)
                    if (t == t2) return true;

            return false;
        }

        public virtual void OnPullEntities() {}
        public virtual void OnStart() {}
        public virtual void OnShutdown() {}
        public virtual void OnUpdate(float deltaTime) {}
        public virtual void OnUpdateEntity(float deltaTime, Entity entity) {}
    }
}
