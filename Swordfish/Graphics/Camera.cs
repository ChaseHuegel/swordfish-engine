using System.Numerics;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;

namespace Swordfish.Graphics;

public class Camera
{
    public Transform Transform { get; set; } = new();

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

    private readonly Transform _viewTransform = new();

    private int _fovDegrees;
    private float _fovRadians;

    public Camera(int fov, float aspectRatio, float nearPlane, float farPlane)
    {
        Fov = fov;
        AspectRatio = aspectRatio;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }

    public Matrix4x4 GetView()
    {
        _viewTransform.Position = Transform.Position;
        _viewTransform.Orientation = Transform.Orientation;
        _viewTransform.Scale = Transform.Scale;

        Matrix4x4.Invert(_viewTransform.ToMatrix4X4(), out Matrix4x4 view);
        return view;
    }

    public Matrix4x4 GetProjection()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(_fovRadians, AspectRatio, NearPlane, FarPlane);
    }

    public Ray ScreenPointToRay(int x, int y, int screenWidth, int screenHeight)
    {
        float ndcX = (2.0f * x / screenWidth) - 1.0f;
        float ndcY = 1.0f - (2.0f * y / screenHeight);

        var clipCoords = new Vector4(ndcX, ndcY, 0f, 1f);

        Matrix4x4.Invert(GetProjection(), out Matrix4x4 invProjection);
        Vector4 worldCoords = Vector4.Normalize(Vector4.Transform(clipCoords, invProjection));

        Matrix4x4.Invert(GetView(), out Matrix4x4 invertedView);
        Vector4 rayWorld = Vector4.Transform(worldCoords, invertedView);

        var rayOrigin = new Vector3(invertedView.M41, invertedView.M42, invertedView.M43);
        Vector3 rayDirection = new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z) - rayOrigin;
        return new Ray(rayOrigin, Vector3.Normalize(rayDirection));
    }
}
