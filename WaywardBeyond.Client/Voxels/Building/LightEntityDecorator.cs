using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class LightEntityDecorator(IBrickDatabase brickDatabase, IPaintDatabase? paintDatabase = null) : IBrickEntityDecorator
{
    /// <inheritdoc/>
    public bool AppliesTo(Brick brick) => brick.Entity?.Light != null;

    /// <inheritdoc/>
    public void OnRemove(in DataStore store, in int entity)
    {
        store.Remove<LightComponent>(entity);
    }

    /// <inheritdoc/>
    public void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo, Brick brick)
    {
        if (brick.Entity?.Light is not BrickLight lightConfig)
        {
            store.Remove<LightComponent>(entity);
            return;
        }

        float radius;
        bool castShadows;
        if (lightConfig.Radius.HasValue)
        {
            if (lightConfig.Radius.Value <= 0f)
            {
                store.Remove<LightComponent>(entity);
                return;
            }

            radius = lightConfig.Radius.Value;
            castShadows = lightConfig.CastShadows ?? false;
        }
        else
        {
            ShapeLight shapeLight = voxelInfo.Voxel.ShapeLight;
            if (shapeLight.LightLevel <= 0 || !IsExposed(voxelComponent.VoxelObject, in voxelInfo))
            {
                store.Remove<LightComponent>(entity);
                return;
            }

            radius = shapeLight.LightLevel;
            castShadows = lightConfig.CastShadows ?? (shapeLight.LightLevel >= 14);
        }

        Vector3 color = ResolveLightColor(voxelComponent.VoxelObject, in voxelInfo, brick, in lightConfig);
        if (store.TryGet(entity, out ChildComponent child))
        {
            Vector3 localPosition = new(voxelInfo.X, voxelInfo.Y, voxelInfo.Z);
            if (lightConfig.Offset != Vector3.Zero)
            {
                Quaternion voxelOrientation = voxelInfo.Voxel.GetOrientation().ToQuaternion();
                localPosition += Vector3.Transform(lightConfig.Offset, voxelOrientation);
            }

            if (child.LocalPosition != localPosition)
            {
                child.LocalPosition = localPosition;
                store.AddOrUpdate(entity, child);
            }
        }

        var light = new LightComponent(
            radius: radius,
            color: color,
            size: lightConfig.Size,
            castShadows: castShadows,
            intensity: lightConfig.Intensity);
        store.AddOrUpdate(entity, light);
    }

    internal Vector3 ResolveLightColor(VoxelObject voxelObject, in VoxelInfo voxelInfo, Brick brick, in BrickLight lightConfig)
    {
        if (!lightConfig.TintFromPaint)
        {
            return lightConfig.Color;
        }

        //  An unpainted light brick emits its authored color, a painted one emits the paint color.
        Vector3 defaultColor = lightConfig.Color * brick.Color;
        if (paintDatabase == null)
        {
            return defaultColor;
        }

        Vector3 colorSum = Vector3.Zero;
        int paintedFaceCount = 0;

        for (var f = 0; f < 6; f++)
        {
            if (!voxelObject.TryGetFaceOverride(voxelInfo.X, voxelInfo.Y, voxelInfo.Z, (BrickFace)f, out FaceOverride faceOverride)
                || faceOverride.ColorPaintId == 0)
            {
                continue;
            }

            Result<Paint> paintResult = paintDatabase.Get(faceOverride.ColorPaintId);
            if (!paintResult.Success || paintResult.Value.Kind != PaintKind.Color)
            {
                continue;
            }

            colorSum += paintResult.Value.Color;
            paintedFaceCount++;
        }

        if (paintedFaceCount == 0)
        {
            return defaultColor;
        }

        Vector3 mixedPaintColor = colorSum / paintedFaceCount;
        return lightConfig.Color * mixedPaintColor;
    }

    private bool IsExposed(VoxelObject voxelObject, in VoxelInfo voxelInfo)
    {
        VoxelSample sample = voxelObject.Sample(voxelInfo.X, voxelInfo.Y, voxelInfo.Z);
        return !IsCuller(sample.Left)
            || !IsCuller(sample.Right)
            || !IsCuller(sample.Above)
            || !IsCuller(sample.Below)
            || !IsCuller(sample.Ahead)
            || !IsCuller(sample.Behind);
    }

    private bool IsCuller(in Voxel voxel)
    {
        return voxel.ID != 0 && brickDatabase.IsCuller(voxel, voxel.GetShapeLight().Shape);
    }
}
