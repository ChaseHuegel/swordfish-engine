using System.Numerics;

namespace Swordfish.ECS;

/// <summary>Represents a global directional light source with an sRGB color.</summary>
public struct DirectionalLightComponent(
    Vector3 direction,
    Vector3 color,
    float intensity = 1.0f,
    bool castShadows = true
) : IDataComponent
{
    public Vector3 Direction = direction;
    public Vector3 Color = color;
    public float Intensity = intensity;
    public bool CastShadows = castShadows;
}
