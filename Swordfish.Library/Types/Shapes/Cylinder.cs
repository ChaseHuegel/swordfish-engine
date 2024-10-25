namespace Swordfish.Library.Types.Shapes
{
    public struct Cylinder
    {
        public float Height;
        public float Radius;

        public Cylinder(float height, float radius)
        {
            Height = height;
            Radius = radius;
        }

        public static implicit operator Shape(Cylinder x) => new(x);
    }
}