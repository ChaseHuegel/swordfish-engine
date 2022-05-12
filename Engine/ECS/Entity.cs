namespace Swordfish.Engine.ECS
{
    public class Entity
    {
        public const int Null = 0;

        public readonly ECSContext Context;
        public readonly int UID;
        public string Name;
        public string Tag;

        /// <summary>
        /// Create an entity with name and tag in provided context
        /// </summary>
        /// <param name="name">Defaults to the entity UID</param>
        /// <param name="tag">Used to group entities</param>
        /// <param name="context">The context this entity exists in</param>
        internal Entity(ECSContext context, string name = "", string tag = "", int uid = -1)
        {
            //  Name should be created from the UID if not provided
            Name = name != "" ? name : uid.ToString();

            Tag = tag;
            Context = context;
            UID = uid;
        }

        //  Casting int to entity and entity to int
        public static implicit operator Entity(int ptr) => new Entity(null, "", "", ptr);
        public static implicit operator int(Entity entity) => entity.UID;

        //  Equals overrides
        public override bool Equals(System.Object obj)
        {
            Entity entity = obj as Entity;

            if (entity == null) return false;

            //  Entities are identified by UID and context (if existing), not reference
            return entity.UID.Equals(this.UID) && (entity.Context == null || entity.Context.Equals(this.Context));
        }

        public override int GetHashCode() => UID.GetHashCode();
    }
}
