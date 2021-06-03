using System.Collections.Generic;
using System;

namespace Swordfish.ECS
{
    public interface IComponentSystem
    {
        void Start();
        void Destroy();
        void Update();
    }
}
