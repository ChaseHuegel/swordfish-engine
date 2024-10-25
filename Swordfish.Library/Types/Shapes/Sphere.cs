namespace Swordfish.Library.Types.Shapes
{
    public struct Sphere
    {
        public float Radius;

        public Sphere(float radius)
        {
            Radius = radius;
        }

        public static implicit operator Shape(Sphere x) => new(x);
    }
}