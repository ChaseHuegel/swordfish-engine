using OpenTK.Mathematics;

namespace Swordfish.Core.Physics
{
    public struct Collision
    {
        public int A, B;
        public Vector3 Contact;
        public Vector3 Normal;
        public float Penetration;
    }
}
