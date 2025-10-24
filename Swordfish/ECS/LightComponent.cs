using System.Numerics;

namespace Swordfish.ECS;

public struct LightComponent : IDataComponent 
{
    public float Radius;
    public Vector3 Color;
    public float Size;

    public LightComponent(float radius, Vector3 color, float size)
    {
        Radius = radius;
        Color = color;
        Size = size;
    }
}