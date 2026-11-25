using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Items;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Systems;

/// <summary>Computes first-person paint sprayer nozzle transforms and brick-face splatter normals.</summary>
internal static class PaintSprayerGeometry
{
    private const int FACE_COUNT = 6;

    private static readonly Vector3 _nozzleLocal = new(0f, -0.006f, -0.85f);

    private static readonly ModelDefinition _defaultViewModel = new()
    {
        Position = new ModelDefinition.Float3(0.5f, -0.5f, -0.3f),
        Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
        Scale = new ModelDefinition.Float3(1f, 1f, 1f),
    };

    /// <summary>Computes the world-space nozzle tip origin for the first-person paint sprayer view model.</summary>
    public static Vector3 ComputeFirstPersonNozzle(in TransformComponent playerTransform, in ModelDefinition? viewModel)
    {
        ModelDefinition model = viewModel ?? _defaultViewModel;
        Quaternion localOrientation = Quaternion.CreateFromYawPitchRoll(
            model.Rotation.Y * MathS.DEGREES_TO_RADIANS,
            model.Rotation.X * MathS.DEGREES_TO_RADIANS,
            model.Rotation.Z * MathS.DEGREES_TO_RADIANS
        );
        var localScale = new Vector3(model.Scale.X, model.Scale.Y, model.Scale.Z);
        var localPos = new Vector3(model.Position.X, model.Position.Y, model.Position.Z);

        Vector3 worldOrigin = playerTransform.Position + Vector3.Transform(localPos, playerTransform.Orientation);
        Quaternion worldOrientation = playerTransform.Orientation * localOrientation;
        Vector3 worldScale = playerTransform.Scale * localScale;
        return worldOrigin + Vector3.Transform(_nozzleLocal * worldScale, worldOrientation);
    }

    /// <summary>Resolves a world-space outward normal and cell-center surface offset for <paramref name="faceMask"/>.</summary>
    public static void ComputeFaceWorldNormalAndOffset(
        BrickFaceMask faceMask,
        Quaternion structureOrientation,
        Orientation voxelOrientation,
        out Vector3 worldNormal,
        out Vector3 surfaceOffset
    ) {
        var localSum = Vector3.Zero;
        var matchedFaces = 0;

        for (var f = 0; f < FACE_COUNT; f++)
        {
            var face = (BrickFace)f;
            if (faceMask.HasFace(face))
            {
                localSum += face.ToLocalNormal();
                matchedFaces++;
            }
        }

        Vector3 structureNormal = Vector3.Transform(localSum, voxelOrientation.ToQuaternion());
        Vector3 transformed = Vector3.Transform(structureNormal, structureOrientation);
        float lenSq = transformed.LengthSquared();
        if (float.IsFinite(lenSq) && lenSq > 1e-6f)
        {
            worldNormal = transformed / MathF.Sqrt(lenSq);
            float distance = matchedFaces == 1 ? 0.5f : 0.35f;
            surfaceOffset = worldNormal * distance;
            return;
        }

        Vector3 fallback = Vector3.Transform(Vector3.UnitY, structureOrientation);
        float fallbackLenSq = fallback.LengthSquared();
        worldNormal = float.IsFinite(fallbackLenSq) && fallbackLenSq > 1e-6f
            ? fallback / MathF.Sqrt(fallbackLenSq)
            : Vector3.UnitY;
        surfaceOffset = Vector3.Zero;
    }
}
