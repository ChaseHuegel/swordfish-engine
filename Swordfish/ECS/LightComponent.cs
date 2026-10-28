using System.Numerics;

namespace Swordfish.ECS;

/// <summary>Represents an omnidirectional point light source.</summary>
public struct LightComponent : IDataComponent 
{
    public float Radius;
    public Vector3 Color;
    public float Size;
    public bool CastShadows;

    public LightComponent(float radius, Vector3 color, float size, bool castShadows = false)
    {
        Radius = radius;
        Color = color;
        Size = size;
        CastShadows = castShadows;
    }
}
