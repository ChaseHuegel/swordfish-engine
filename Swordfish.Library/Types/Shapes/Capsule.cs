namespace Swordfish.Library.Types.Shapes
{
    public struct Capsule : IShape
    {
        public float Height;
        public float Radius;

        public Capsule(float height, float radius)
        {
            Height = height;
            Radius = radius;
        }
    }
}