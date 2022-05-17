using OpenTK.Mathematics;

namespace Swordfish.Library.Types
{
    public class Color
    {
        //  -----------------------------------------------------
        //  --- Predefined colors ---
        public static Color White       = new Color(1f, 1f, 1f, 1f);
        public static Color Black       = new Color(0f, 0f, 0f, 1f);

        public static Color Gray        = new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color Grey         => Gray;

        public static Color Clear       = new Color(0f, 0f, 0f, 0f);
        public static Color Transparent => Clear;

        public static Color Red         = new Color(1f, 0f, 0f, 1f);
        public static Color Green       = new Color(0f, 1f, 0f, 1f);
        public static Color Blue        = new Color(0f, 0f, 1f, 1f);
        //  -----------------------------------------------------



        public float r, g, b, a;

        public float R => r;
        public float G => g;
        public float B => b;
        public float A => a;

        public Vector3 rgb => new Vector3(r, g, b);
        public Vector3 RGB => rgb;

        public Color() : this(1f, 1f, 1f, 1f) {}
        public Color(Vector4 v) : this(v.X, v.Y, v.Z, v.W) {}
        public Color(System.Numerics.Vector4 v) : this(v.X, v.Y, v.Z, v.W) {}

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        //  Implicit cast to/from OGL vector4
        public static implicit operator Color(Vector4 v) => new Color(v);
        public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);

        //  Implicit cast to/from system vector4
        public static implicit operator Color(System.Numerics.Vector4 v) => new Color(v);
        public static implicit operator System.Numerics.Vector4(Color c) => new System.Numerics.Vector4(c.r, c.g, c.b, c.a);
    }
}
