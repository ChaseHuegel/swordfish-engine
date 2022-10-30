namespace Swordfish.Library.Types
{
    public struct Vec2f
    {
        public static Vec2f Zero { get; } = new Vec2f(0f, 0f);

        public float X;
        public float Y;

        public Vec2f(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
