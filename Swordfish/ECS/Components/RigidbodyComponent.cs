using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    [Component]
    public struct RigidbodyComponent
    {
        public float mass;
        public float resistance;

        public Vector3 velocity;
        public float drag;
    }
}
