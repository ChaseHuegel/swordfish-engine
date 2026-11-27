using System;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Systems;

/// <summary>Computes laser beam origins and orientations in world space.</summary>
internal static class LaserBeamGeometry
{
    private static readonly Vector3 _barrelNozzleLocal = new(0f, 0f, -1.125f);

    private static readonly ModelDefinition _defaultViewModel = new()
    {
        Position = new ModelDefinition.Float3(0.4f, -0.4f, -0.5f),
        Rotation = new ModelDefinition.Float3(0f, -60f, 0f),
        Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
    };

    /// <summary>Computes the world-space barrel nozzle origin for a first-person view-model child transform.</summary>
    public static Vector3 ComputeFirstPersonMuzzle(in TransformComponent playerTransform, in ChildComponent viewModelChild)
    {
        return ViewModelGeometry.ComputeWorldPoint(in playerTransform, in viewModelChild, in _barrelNozzleLocal);
    }

    /// <summary>Computes the world-space barrel nozzle origin for the first-person view model.</summary>
    public static Vector3 ComputeFirstPersonMuzzle(in TransformComponent playerTransform, in ModelDefinition? viewModel)
    {
        ModelDefinition model = viewModel ?? _defaultViewModel;
        return ViewModelGeometry.ComputeWorldPoint(in playerTransform, in model, in _barrelNozzleLocal);
    }

    /// <summary>Builds a right-handed orientation whose local -Z axis points along <paramref name="direction"/>.</summary>
    public static Quaternion CreateBeamOrientation(Vector3 direction)
    {
        float lenSq = direction.LengthSquared();
        Vector3 unitDir = float.IsFinite(lenSq) && lenSq > 1e-6f
            ? direction / MathF.Sqrt(lenSq)
            : -Vector3.UnitZ;

        Vector3 forward = -unitDir;
        Vector3 upRef = MathF.Abs(forward.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 right = Vector3.Normalize(Vector3.Cross(upRef, forward));
        Vector3 up = Vector3.Cross(forward, right);

        var matrix = new Matrix4x4(
            right.X,   right.Y,   right.Z,   0f,
            up.X,      up.Y,      up.Z,      0f,
            forward.X, forward.Y, forward.Z, 0f,
            0f,        0f,        0f,        1f
        );

        return Quaternion.CreateFromRotationMatrix(matrix);
    }
}
