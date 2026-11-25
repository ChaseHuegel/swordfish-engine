using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Extensions;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Client.Voxels.Building;

internal sealed class ThrusterEntityDecorator : IVoxelEntityDecorator
{
    private static readonly Vector3 _lightColor = Color.FromArgb(red: 244, green: 126, blue: 27).ToVector3();

    private readonly HashSet<ushort> _thrusterBrickIDs;

    public ThrusterEntityDecorator(in IBrickRegistry brickRegistry, in ILocalizedTagsDatabase tagDatabase)
    {
        _thrusterBrickIDs = [];
        IReadOnlyList<string>? thrusters = tagDatabase.Invariant?.GetValues(BrickTags.THRUSTER);
        if (thrusters == null)
        {
            return;
        }

        for (var i = 0; i < thrusters.Count; i++)
        {
            ushort id = brickRegistry.Id(thrusters[i]);
            if (id != 0)
            {
                _thrusterBrickIDs.Add(id);
            }
        }
    }

    public void Process(in DataStore store, in int parent, in int entity, in VoxelComponent voxelComponent, in VoxelInfo voxelInfo)
    {
        if (!_thrusterBrickIDs.Contains(voxelInfo.Voxel.ID))
        {
            return;
        }

        var light = new LightComponent(radius: 0.75f, color: _lightColor, size: 0.25f, intensity: 20f);
        store.AddOrUpdate(entity, light);
    }
}
