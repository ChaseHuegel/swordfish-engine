namespace Swordfish.Library.Types.Shapes
{
    public struct Sphere : IShape
    {
        public float Radius;

        public Sphere(float radius)
        {
            Radius = radius;
        }
    }
}