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
        ViewTransform.Rotation = Transform.Rotation;
        //  Reflect the camera's Z scale so +Z extends away from the viewer
        ViewTransform.Scale = new Vector3(Transform.Scale.X, Transform.Scale.Y, Transform.Scale.Z);

        Matrix4x4.Invert(ViewTransform.ToMatrix4x4(), out Matrix4x4 view);

        return view;
    }

    public Matrix4x4 GetProjection()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(FOVRadians, AspectRatio, NearPlane, FarPlane);
    }

    public Ray ScreenPointToRay(int x, int y, int screenWidth, int screenHeight)
    {
        // Step 1: Convert screen coordinates to normalized device coordinates (NDC)
        float ndcX = (2.0f * x / screenWidth) - 1.0f;
        float ndcY = 1.0f - (2.0f * y / screenHeight);
        float ndcZ = 0.0f; // You can set this to 1.0 for the far plane

        // Step 2: Convert NDC to Homogeneous Clip Coordinates
        var clipCoords = new Vector4(ndcX, ndcY, ndcZ, 1f);

        // Step 3: Convert Clip Coordinates to World Coordinates
        Matrix4x4.Invert(GetProjection(), out Matrix4x4 invProjection);
        var worldCoords = Vector4.Transform(clipCoords, invProjection);

        // Step 4: Normalize the coordinates
        worldCoords /= worldCoords.W;

        // Step 5: Convert to view space
        Matrix4x4.Invert(GetView(), out Matrix4x4 invView);
        // var invView = GetView();
        var rayWorld = Vector4.Transform(worldCoords, invView);

        // Step 6: Create a ray from the camera's position
        var rayOrigin = new Vector3(invView.M41, invView.M42, invView.M43); // Camera position
        var rayDirection = new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z) - rayOrigin;

        return new Ray(rayOrigin, Vector3.Normalize(rayDirection));
    }
}
