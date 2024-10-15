using System.Numerics;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;

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

    private readonly Transform ViewTransform = new();

    private int FOVDegrees;
    private float FOVRadians;

    public Camera(int fov, float aspectRatio, float nearPlane, float farPlane)
    {
        FOV = fov;
        AspectRatio = aspectRatio;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }

    public Matrix4x4 GetView()
    {
        ViewTransform.Position = Transform.Position;
        ViewTransform.Orientation = Transform.Orientation;
        ViewTransform.Scale = Transform.Scale;

        Matrix4x4.Invert(ViewTransform.ToMatrix4x4(), out Matrix4x4 view);
        return view;
    }

    public Matrix4x4 GetProjection()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(FOVRadians, AspectRatio, NearPlane, FarPlane);
    }

    public Ray ScreenPointToRay(int x, int y, int screenWidth, int screenHeight)
    {
        float ndcX = (2.0f * x / screenWidth) - 1.0f;
        float ndcY = 1.0f - (2.0f * y / screenHeight);

        var clipCoords = new Vector4(ndcX, ndcY, 0f, 1f);

        Matrix4x4.Invert(GetProjection(), out Matrix4x4 invProjection);
        var worldCoords = Vector4.Normalize(Vector4.Transform(clipCoords, invProjection));

        Matrix4x4.Invert(GetView(), out Matrix4x4 invertedView);
        var rayWorld = Vector4.Transform(worldCoords, invertedView);

        var rayOrigin = new Vector3(invertedView.M41, invertedView.M42, invertedView.M43);
        var rayDirection = new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z) - rayOrigin;
        return new Ray(rayOrigin, Vector3.Normalize(rayDirection));
    }
}
