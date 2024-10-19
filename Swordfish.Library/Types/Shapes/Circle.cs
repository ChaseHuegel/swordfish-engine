namespace Swordfish.Library.Types.Shapes
{
    public struct Circle : IShape
    {
        public float Radius;

        public Circle(float radius)
        {
            Radius = radius;
        }
    }
}