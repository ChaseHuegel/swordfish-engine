using System;
using Swordfish.Containers;

namespace Swordfish.ECS
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

        protected Entity[] entities = new Entity[0];
        internal void PullEntities() => entities = Engine.ECS.Pull(filter);

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

        public virtual void OnStart() {}
        public virtual void OnShutdown() {}
        public virtual void OnUpdate() {}
    }
}
