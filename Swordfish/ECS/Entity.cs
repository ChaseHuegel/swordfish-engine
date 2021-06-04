using System;

namespace Swordfish.ECS
{
    public class Entity
    {
        public readonly ECSContext Context;
        public readonly int? UID;
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

            UID = Context?.CreateEntityUID();

            if (Context == null) Debug.Log("entity created without context!", "ECS", LogType.ERROR);
            if (UID == null) Debug.Log("null entity UID!", "ECS", LogType.ERROR);
        }
    }
}
