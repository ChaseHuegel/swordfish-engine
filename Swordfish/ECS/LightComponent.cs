using System.Numerics;
using Krill;

namespace Swordfish.ECS;

/// <summary>Represents an omnidirectional point light source with an sRGB color.</summary>
public struct LightComponent : IDataComponent 
{
    public float Radius;
    public Vector3 Color;
    public float Size;
    public bool CastShadows;
    public float Intensity;

    public LightComponent(float radius, Vector3 color, float size, bool castShadows = false, float intensity = 1.0f)
    {
        Radius = radius;
        Color = color;
        Size = size;
        CastShadows = castShadows;
        Intensity = intensity;
    }
}
