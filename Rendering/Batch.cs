using System.Collections.Generic;
using Swordfish.ECS;

namespace Swordfish.Rendering
{
    internal class Batch
    {
        private List<int> Entities;

        public Batch()
        {
            Entities = new List<int>();
        }

        public int[] GetEntities() => Entities.ToArray();

        public void Add(int entity) => Entities.Add(entity);

    }
}
