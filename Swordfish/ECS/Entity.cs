using System;

namespace Swordfish.ECS
{
    public class Entity
    {
        public readonly ECSContext Context;
        public readonly int UID;
        public readonly string Name;
        public readonly string Tag;

        /// <summary>
        /// Create an entity with name and tag in provided context
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <param name="context"></param>
        internal Entity(ECSContext context, string name = "", string tag = "")
        {
            Name = name;
            Tag = tag;
            Context = context;

            if (Context != null)
                UID = Context.CreateEntityUID();
            else
                Debug.Log("entity created without context!", "ECS", LogType.ERROR);
        }

        //  Equals overrides
        public override bool Equals(System.Object obj)
        {
            Entity entity = obj as Entity;

            if (entity == null) return false;

            return entity.UID.Equals(this.UID);
        }

        public override int GetHashCode() => UID.GetHashCode();
    }
}
