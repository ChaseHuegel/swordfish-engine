using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Result of validating a paint-apply target against brick paintability and alpha-bucket rules.</summary>
public enum PaintTargetValidation : byte
{
    Valid = 0,
    EmptyCell = 1,
    UnknownBrick = 2,
    NotPaintable = 3,
    MissingPaint = 4,
    AlphaMismatch = 5,
}

/// <summary>
/// Shared validation for per-face brick painting (client prediction and server authority).
/// Enforces brick paintability, like-material alpha matching for texture paints, and shape face expansion.
/// </summary>
public static class PaintInteractionResolver
{
    private static readonly Vector3 _halfCell = new(0.5f);

    /// <summary>Validates a paint interaction request and resolves its target cell, expanded face mask, and paint id.</summary>
    public static InteractionResolution Resolve(
        in InteractionRequest request,
        in IVoxelInteractionWorld world,
        IBrickDatabase brickDatabase,
        IPaintDatabase paintDatabase,
        IPaintRegistry paintRegistry
    ) {
        if (request.PaintHint == null)
        {
            return InteractionResolution.None;
        }

        bool isApply = request.Kind == InteractionKind.PrimaryPressed;
        bool isClear = request.Kind == InteractionKind.SecondaryPressed;
        if (!isApply && !isClear)
        {
            return InteractionResolution.None;
        }

        PaintInteraction hint = request.PaintHint.Value;
        if (hint.Mode > (byte)PaintKind.Color)
        {
            return InteractionResolution.None;
        }

        var mode = (PaintKind)hint.Mode;

        Result<VoxelTarget> targetResult = world.GetTarget(Uuid.FromValue(hint.TargetEntity));
        if (!targetResult.Success || targetResult.Value.VoxelObject == null)
        {
            return InteractionResolution.None;
        }

        VoxelObject voxelObject = targetResult.Value.VoxelObject;
        TransformComponent transform = targetResult.Value.Transform;
        int entity = targetResult.Value.Entity;
        Int3 hintCell = new(hint.TargetX, hint.TargetY, hint.TargetZ);

        if (!IsReachable(hintCell, request.Origin, in transform, request.Reach))
        {
            return InteractionResolution.None;
        }

        Voxel targetVoxel = voxelObject.Get(hintCell.X, hintCell.Y, hintCell.Z);
        if (targetVoxel.ID == 0)
        {
            return InteractionResolution.None;
        }

        Result<Brick> brickResult = brickDatabase.Get(targetVoxel.ID);
        if (!brickResult.Success || !brickResult.Value.Paintable)
        {
            return InteractionResolution.None;
        }

        BrickShape shape = targetVoxel.GetShapeLight().Shape;
        BrickFaceMask expandedMask = ExpandFaceMask((BrickFaceMask)hint.FaceMask, shape);
        if (expandedMask == BrickFaceMask.None)
        {
            return InteractionResolution.None;
        }

        if (isClear)
        {
            return new InteractionResolution(entity, hintCell, new PaintResolution(expandedMask, mode, paintId: 0, canonicalPaintId: null));
        }

        if (ValidateApplyTarget(in targetVoxel, mode, hint.PaintId, brickDatabase, paintDatabase, paintRegistry, out ushort paintId, out Paint? paint) != PaintTargetValidation.Valid
            || paint == null)
        {
            return InteractionResolution.None;
        }

        return new InteractionResolution(
            entity,
            hintCell,
            new PaintResolution(expandedMask, mode, paintId, paint.ID)
        );
    }

    /// <summary>Validates whether a targeted voxel can receive the selected paint in the given mode.</summary>
    public static PaintTargetValidation ValidateApplyTarget(
        in Voxel targetVoxel,
        PaintKind mode,
        string? paintId,
        IBrickDatabase brickDatabase,
        IPaintDatabase paintDatabase,
        IPaintRegistry paintRegistry
    ) {
        return ValidateApplyTarget(in targetVoxel, mode, paintId, brickDatabase, paintDatabase, paintRegistry, out _, out _);
    }

    /// <summary>Returns true when at least one face in <paramref name="expandedMask"/> has an override for <paramref name="mode"/>.</summary>
    public static bool HasRemovableOverride(VoxelObject voxelObject, in Int3 coordinate, BrickFaceMask expandedMask, PaintKind mode)
    {
        if (expandedMask == BrickFaceMask.None)
        {
            return false;
        }

        for (var f = 0; f < 6; f++)
        {
            var face = (BrickFace)f;
            if ((expandedMask & face.ToMask()) == 0)
            {
                continue;
            }

            if (!voxelObject.TryGetFaceOverride(coordinate.X, coordinate.Y, coordinate.Z, face, out FaceOverride faceOverride))
            {
                continue;
            }

            if (mode == PaintKind.Texture ? faceOverride.TexturePaintId != 0 : faceOverride.ColorPaintId != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the eye-dropper paint for a targeted brick face in the given tool mode:
    /// Color mode picks the face's painted color override; Texture mode picks the face's painted
    /// texture override or falls back to a texture paint matching the target brick's base texture on that face.
    /// </summary>
    public static Result<Paint> PickFacePaint(
        VoxelObject voxelObject,
        in Int3 coordinate,
        in Voxel targetVoxel,
        BrickFace hitFace,
        PaintKind mode,
        IBrickDatabase brickDatabase,
        IPaintDatabase paintDatabase
    ) {
        if (targetVoxel.ID == 0)
        {
            return Result<Paint>.FromFailure("Target cell is empty.");
        }

        bool hasOverride = voxelObject.TryGetFaceOverride(coordinate.X, coordinate.Y, coordinate.Z, hitFace, out FaceOverride faceOverride);

        if (mode == PaintKind.Color)
        {
            if (hasOverride && faceOverride.ColorPaintId != 0)
            {
                Result<Paint> colorResult = paintDatabase.Get(faceOverride.ColorPaintId);
                if (colorResult.Success && colorResult.Value.Kind == PaintKind.Color)
                {
                    return colorResult;
                }
            }

            return Result<Paint>.FromFailure("Target face has no painted color.");
        }

        if (hasOverride && faceOverride.TexturePaintId != 0)
        {
            Result<Paint> textureResult = paintDatabase.Get(faceOverride.TexturePaintId);
            if (textureResult.Success && textureResult.Value.Kind == PaintKind.Texture)
            {
                return textureResult;
            }
        }

        Result<Brick> brickResult = brickDatabase.Get(targetVoxel.ID);
        if (!brickResult.Success)
        {
            return Result<Paint>.FromFailure("Unknown target brick.");
        }

        Brick brick = brickResult.Value;
        string?[]? faceTextures = GetFaceTextures(in brick.Textures, hitFace);
        if (faceTextures == null || faceTextures.Length == 0)
        {
            return Result<Paint>.FromFailure("Target brick face has no base texture.");
        }

        Paint? bestMatch = FindMatchingTexturePaint(paintDatabase.TexturePaints, brick, faceTextures);
        return bestMatch != null
            ? Result<Paint>.FromSuccess(bestMatch)
            : Result<Paint>.FromFailure("No texture paint matches the target brick face.");
    }

    private static string?[]? GetFaceTextures(in BrickTextures textures, BrickFace face)
    {
        string?[]? faceTextures = face switch
        {
            BrickFace.Top => textures.Top,
            BrickFace.Bottom => textures.Bottom,
            BrickFace.Front => textures.Front,
            BrickFace.Back => textures.Back,
            BrickFace.Left => textures.Left,
            BrickFace.Right => textures.Right,
            _ => null,
        };

        return faceTextures is { Length: > 0 } ? faceTextures : textures.Default;
    }

    private static Paint? FindMatchingTexturePaint(IReadOnlyList<Paint> texturePaints, Brick brick, string?[] faceTextures)
    {
        Paint? bestMatch = null;
        int bestScore = -1;

        for (var i = 0; i < texturePaints.Count; i++)
        {
            Paint candidate = texturePaints[i];
            if (candidate.Textures == null || candidate.Textures.Length == 0)
            {
                continue;
            }

            if (!SharesAnyTexture(faceTextures, candidate.Textures))
            {
                continue;
            }

            int score = 0;
            if (candidate.Alpha == brick.Alpha)
            {
                score += 4;
            }

            if (candidate.Connected == brick.Textures.Connected)
            {
                score += 2;
            }

            if (HasExactTextureSet(faceTextures, candidate.Textures))
            {
                score += 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }

    private static bool SharesAnyTexture(string?[] faceTextures, string?[] paintTextures)
    {
        for (var i = 0; i < faceTextures.Length; i++)
        {
            string? faceTexture = faceTextures[i];
            if (string.IsNullOrWhiteSpace(faceTexture))
            {
                continue;
            }

            for (var j = 0; j < paintTextures.Length; j++)
            {
                if (string.Equals(faceTexture, paintTextures[j], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasExactTextureSet(string?[] left, string?[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static PaintTargetValidation ValidateApplyTarget(
        in Voxel targetVoxel,
        PaintKind mode,
        string? paintId,
        IBrickDatabase brickDatabase,
        IPaintDatabase paintDatabase,
        IPaintRegistry paintRegistry,
        out ushort numericPaintId,
        out Paint? paint
    ) {
        numericPaintId = 0;
        paint = null;

        if (targetVoxel.ID == 0)
        {
            return PaintTargetValidation.EmptyCell;
        }

        Result<Brick> brickResult = brickDatabase.Get(targetVoxel.ID);
        if (!brickResult.Success)
        {
            return PaintTargetValidation.UnknownBrick;
        }

        if (!brickResult.Value.Paintable)
        {
            return PaintTargetValidation.NotPaintable;
        }

        if (string.IsNullOrEmpty(paintId))
        {
            return PaintTargetValidation.MissingPaint;
        }

        numericPaintId = paintRegistry.Id(paintId);
        if (numericPaintId == 0)
        {
            return PaintTargetValidation.MissingPaint;
        }

        Result<Paint> paintResult = paintDatabase.Get(numericPaintId);
        if (!paintResult.Success || paintResult.Value.Kind != mode)
        {
            numericPaintId = 0;
            return PaintTargetValidation.MissingPaint;
        }

        //  Texture paints must match the target brick's alpha bucket (opaque, masked, or blend).
        if (mode == PaintKind.Texture && paintResult.Value.Alpha != brickResult.Value.Alpha)
        {
            numericPaintId = 0;
            return PaintTargetValidation.AlphaMismatch;
        }

        paint = paintResult.Value;
        return PaintTargetValidation.Valid;
    }

    /// <summary>Expands a targeted brick-local face mask according to the brick's shape rules.</summary>
    public static BrickFaceMask ExpandFaceMask(BrickFaceMask requested, BrickShape shape)
    {
        BrickFaceMask masked = requested & BrickFaceMask.All;
        if (masked == BrickFaceMask.None)
        {
            return BrickFaceMask.None;
        }

        if (shape == BrickShape.Custom)
        {
            return BrickFaceMask.All;
        }

        if (shape == BrickShape.Slope && (masked & (BrickFaceMask.Front | BrickFaceMask.Top)) != 0)
        {
            return masked | BrickFaceMask.Front | BrickFaceMask.Top;
        }

        return masked;
    }

    /// <summary>Converts a world-space raycast hit normal into a brick-local <see cref="BrickFace"/>.</summary>
    public static BrickFace WorldNormalToBrickFace(Vector3 worldNormal, Quaternion structureOrientation, Orientation voxelOrientation)
    {
        Vector3 structureNormal = Vector3.Transform(worldNormal, Quaternion.Inverse(structureOrientation));
        Vector3 brickLocalNormal = Vector3.Transform(structureNormal, Quaternion.Inverse(voxelOrientation.ToQuaternion()));
        return BrickFaceExtensions.FromLocalNormal(brickLocalNormal);
    }

    private static bool IsReachable(in Int3 coordinate, Vector3 origin, in TransformComponent transform, float reach)
    {
        Vector3 localOrigin = Vector3.Transform(origin - transform.Position, Quaternion.Inverse(transform.Orientation));
        var center = new Vector3(coordinate.X, coordinate.Y, coordinate.Z);
        Vector3 nearest = Vector3.Clamp(localOrigin, center - _halfCell, center + _halfCell);
        return Vector3.DistanceSquared(localOrigin, nearest) <= reach * reach;
    }
}
