namespace WaywardBeyond.Client.Core.Items;

public struct ModelDefinition()
{
    public string Mesh;
    public string Material;
    public Float3 Position;
    public Float3 Rotation;
    public Float3 Scale = new(1f, 1f, 1f);

    public struct Float3()
    {
        public float X;
        public float Y;
        public float Z;
        
        public Float3(float x, float y, float z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}