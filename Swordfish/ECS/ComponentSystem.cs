using System;
using Swordfish.Containers;

namespace Swordfish.ECS
{
    public class ComponentSystem
    {
        public static Bitmask64 filter;
        public void AssignFilter(Bitmask64 mask) => filter = mask;

        public virtual void OnCreate() {}
        public virtual void OnDestroy() {}
        public virtual void OnUpdate() {}
    }
}
