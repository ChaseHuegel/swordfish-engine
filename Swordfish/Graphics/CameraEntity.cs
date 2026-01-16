using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;

namespace Swordfish.Graphics;

public readonly struct CameraEntity(in Entity entity, in ViewFrustumComponent viewFrustum, in TransformComponent transform)
{
    public readonly Entity Entity = entity;
    public readonly ViewFrustumComponent ViewFrustum = viewFrustum;
    public readonly TransformComponent Transform = transform;

    public Matrix4x4 GetView()
    {
        Matrix4x4.Invert(Transform.ToMatrix4X4(), out Matrix4x4 view);
        return view;
    }

    public Matrix4x4 GetProjection(float aspectRatio)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(ViewFrustum.FOV.Radians, aspectRatio, ViewFrustum.NearPlane, ViewFrustum.FarPlane);
    }

    public Ray ScreenPointToRay(int x, int y, int screenWidth, int screenHeight)
    {
        float aspectRatio = (float)screenWidth / screenHeight;
        
        float ndcX = 2.0f * x / screenWidth - 1.0f;
        float ndcY = 1.0f - 2.0f * y / screenHeight;

        var clipFar = new Vector4(ndcX, ndcY, 1f, 1f);

        Matrix4x4.Invert(GetProjection(aspectRatio), out Matrix4x4 invProjection);
        Vector4 eyeFar = Vector4.Transform(clipFar, invProjection);
        eyeFar /= eyeFar.W;

        Matrix4x4.Invert(GetView(), out Matrix4x4 invertedView);
        Vector4 worldFar = Vector4.Transform(eyeFar, invertedView);

        var rayOrigin = new Vector3(invertedView.M41, invertedView.M42, invertedView.M43);
        Vector3 rayDirection = Vector3.Normalize(new Vector3(worldFar.X, worldFar.Y, worldFar.Z) - rayOrigin);
        return new Ray(rayOrigin, rayDirection);
    }
}
