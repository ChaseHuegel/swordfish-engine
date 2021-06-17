namespace Swordfish.ECS
{
    public class Entity
    {
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

        //  Equals overrides
        public override bool Equals(System.Object obj)
        {
            Entity entity = obj as Entity;

            if (entity == null) return false;

            //  Entities are identified by UID and context, not reference
            return entity.UID.Equals(this.UID) && entity.Context.Equals(this.Context);
        }

        public override int GetHashCode() => UID.GetHashCode();
    }
}
