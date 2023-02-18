using OpenTK.Mathematics;

namespace Swordfish.Engine.ECS
{
    [Component]
    public struct LightComponent
    {
        public Vector4 color;
        public float lumens;
        public float range;
    }
}
