using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    [Component]
    public struct LightComponent
    {
        public Vector4 color;
        public float intensity;
        public float range;
    }
}
