using OpenTK.Mathematics;

namespace Swordfish.Core.ECS
{
    [Component]
    public struct LightComponent
    {
        public Vector4 color;
        public float lumens;
        public float range;
    }
}
