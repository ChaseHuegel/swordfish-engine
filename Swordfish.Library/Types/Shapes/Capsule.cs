namespace Swordfish.Library.Types.Shapes
{
    public struct Capsule
    {
        public float Height;
        public float Radius;

        public Capsule(float height, float radius)
        {
            Height = height;
            Radius = radius;
        }

        public static implicit operator Shape(Capsule x) => new(x);
    }
}