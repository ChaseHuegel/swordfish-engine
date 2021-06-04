using System.Text;
using System.Globalization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Swordfish.ECS
{
    public class EcsContext
    {
        private ComponentSystems systems;
        private ExpandingList<Entity> entities;
        private Queue recycledIDs;

        private Dictionary<ushort, HashSet<ushort>> componentMappings;

        public EcsContext()
        {
            //  Components must be registered before systems
            Component.RegisterComponents();
            ComponentSystems.RegisterSystems();

            systems = new ComponentSystems(this);
            entities = new ExpandingList<Entity>();
            recycledIDs = new Queue();

            //  Dictionary mapping components to a list of matching entities
            componentMappings = new Dictionary<ushort, HashSet<ushort>>();
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

            systems.Start();

            return entities[entities.Count];
        }

        public void PushEntity(Entity entity)
        {
            entity.ID = CreateEntityID();
            entity.Context = this;
            entity.PushComponents();

            entities.Add(entity);

            systems.Start();
        }

        public void PushComponent(Entity entity, ushort componentID)
        {
            HashSet<ushort> e;
            if (componentMappings.TryGetValue(componentID, out e))
                e.Add(entity.ID);
            else
            {
                e = new HashSet<ushort>();
                e.Add(entity.ID);
                componentMappings.TryAdd(componentID, e);
            }
        }

        public void DestroyEntity(Entity entity)
        {
            entities[entity.ID] = null;
            recycledIDs.Enqueue(entity.ID);
        }

        public Entity GetEntityWith(BitMask filter)
        {
            List<ushort> componentIDs = new List<ushort>();

            //  Convert BitMask indicies to componentIDs
            for (int i = 0; i < filter.Length; i++)
                if (filter[i])
                    componentIDs.Add((ushort)i);

            return GetEntityWith(componentIDs.ToArray());
        }

        public Entity GetEntityWith(params Type[] components)
        {
            ushort[] componentIDs = new ushort[components.Length];

            for (int i = 0; i < componentIDs.Length; i++)
                componentIDs[i] = Component.Get(components[i]);

            return GetEntityWith(componentIDs);
        }

        public Entity GetEntityWith(params ushort[] componentIDs)
        {
            List<HashSet<ushort>> entityLists = new List<HashSet<ushort>>();
            HashSet<ushort> e;

            //  Grab the list of entities containing each component
            for (int i = 0; i < componentIDs.Length; i++)
            {
                componentMappings.TryGetValue(componentIDs[i], out e);
                entityLists.Add(e);
            }

            if (entityLists[0] == null)
                return null;

            //  Test every entity in the first list
            foreach (ushort id in entityLists[0])
            {
                //  Get the # of matches to this entity within other lists
                int matches = 1;
                for (int i = 1; i < entityLists.Count; i++)
                    if (entityLists[i] != null && entityLists[i].Contains(id))
                        matches++;

                //  If # of matches is # of components, we have a matching entity
                if (matches == componentIDs.Length)
                    return entities[(int)id];
            }

            return null;
        }

        public Entity[] GetEntitiesWith(BitMask filter)
        {
            List<ushort> componentIDs = new List<ushort>();

            //  Convert BitMask indicies to componentIDs
            for (int i = 0; i < filter.Length; i++)
                if (filter[i])
                    componentIDs.Add((ushort)i);

            return GetEntitiesWith(componentIDs.ToArray());
        }

        public Entity[] GetEntitiesWith(params Type[] components)
        {
            ushort[] componentIDs = new ushort[components.Length];

            for (int i = 0; i < componentIDs.Length; i++)
                componentIDs[i] = Component.Get(components[i]);

            return GetEntitiesWith(componentIDs);
        }

        public Entity[] GetEntitiesWith(params ushort[] componentIDs)
        {
            List<Entity> matchingEntities = new List<Entity>();

            List<HashSet<ushort>> entityLists = new List<HashSet<ushort>>();
            HashSet<ushort> e;

            //  Grab the list of entities containing each component
            for (int i = 0; i < componentIDs.Length; i++)
            {
                componentMappings.TryGetValue(componentIDs[i], out e);
                entityLists.Add(e);
            }

            if (entityLists[0] == null)
                return new Entity[0];

            //  Test every entity in the first list
            foreach (ushort id in entityLists[0])
            {
                //  Get the # of matches to this entity within other lists
                int matches = 1;
                for (int i = 1; i < entityLists.Count; i++)
                    if (entityLists[i] != null && entityLists[i].Contains(id))
                        matches++;

                //  If # of matches is # of components, we have a matching entity
                if (matches == componentIDs.Length)
                    matchingEntities.Add(entities[(int)id]);
            }

            return matchingEntities.ToArray();
        }
    }
}
