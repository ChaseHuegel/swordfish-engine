using System;

namespace WaywardBeyond.Client.Core.Items;

public record struct ModelDefinition()
{
    public string Mesh;
    public string Material;
    public Float3 Position;
    public Float3 Rotation;
    public Float3 Scale = new(1f, 1f, 1f);

    public bool Equals(ModelDefinition other)
    {
        return other.Mesh == Mesh && other.Material == Material;
    }

    public readonly override int GetHashCode()
    {
        return HashCode.Combine(Mesh, Material);
    }

    public record struct Float3()
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