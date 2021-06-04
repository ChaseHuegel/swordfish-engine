using System.Collections.Generic;
using System;

namespace Swordfish.ECS_OLD
{
    public class ComponentSystem
    {
        public static BitMask filter;
        public void AssignFilter(BitMask mask) => filter = mask;

        public virtual void Start() {}
        public virtual void Destroy() {}
        public virtual void Update() {}
    }
}
