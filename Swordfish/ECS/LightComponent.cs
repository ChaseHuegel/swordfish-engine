using System.Numerics;

namespace Swordfish.ECS;

public struct LightComponent : IDataComponent 
{
    public float Radius;
    public Vector3 Color;
    public float Intensity;

    public LightComponent(float radius, Vector3 color, float intensity)
    {
        Radius = radius;
        Color = color;
        Intensity = intensity;
    }
}