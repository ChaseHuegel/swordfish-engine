namespace Swordfish.Library.Types.Shapes
{
    public struct Circle
    {
        public float Radius;

        public Circle(float radius)
        {
            Radius = radius;
        }

        public static implicit operator Shape(Circle x) => new(x);
    }
}