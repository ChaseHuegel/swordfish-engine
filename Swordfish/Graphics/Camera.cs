using System.Numerics;
using Swordfish.Library.Types;
using Swordfish.Library.Util;

namespace Swordfish.Graphics;

public class Camera
{
    public Transform Transform { get; set; } = new();

    public int FOV
    {
        get => FOVDegrees;
        set
        {
            FOVDegrees = value;
            FOVRadians = MathS.DegreesToRadians * value;
        }
    }

    public float AspectRatio { get; set; }
    public float NearPlane { get; set; }
    public float FarPlane { get; set; }

    private int FOVDegrees;
    private float FOVRadians;

    public Camera(int fov, float aspectRatio, float nearPlane, float farPlane)
    {
        FOV = fov;
        AspectRatio = aspectRatio;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }

    public Matrix4x4 GetProjection()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(FOVRadians, AspectRatio, NearPlane, FarPlane);
    }
}
