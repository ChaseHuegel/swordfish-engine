namespace Swordfish.Library.Types.Shapes
{
    public struct Cylinder : IShape
    {
        public float Height;
        public float Radius;

        public Cylinder(float height, float radius)
        {
            Height = height;
            Radius = radius;
        }
    }
}