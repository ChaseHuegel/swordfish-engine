using OpenTK.Mathematics;

namespace Swordfish.Physics
{
    public class CollisionInfo
    {
        public Vector3[] Contacts;
        public Vector3 Normal;
        public float Penetration;

        public Vector3 Response { get => Normal * Penetration; }
    }
}
