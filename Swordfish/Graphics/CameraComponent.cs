using Swordfish.ECS;
using Swordfish.Library.Util;

namespace Swordfish.Graphics;

public struct CameraComponent : IDataComponent
{
    public int Fov
    {
        get => _fovDegrees;
        set
        {
            _fovDegrees = value;
            _fovRadians = MathS.DEGREES_TO_RADIANS * value;
        }
    }

    public float AspectRatio { get; set; }
    public float NearPlane { get; set; }
    public float FarPlane { get; set; }

    private int _fovDegrees;
    private float _fovRadians;
}