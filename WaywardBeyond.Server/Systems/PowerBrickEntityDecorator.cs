using System;
using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Power;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Built-in server decorator that attaches a <see cref="PowerNodeComponent"/> to power-producing or
/// power-consuming bricks.
/// </summary>
public sealed class PowerBrickEntityDecorator : IServerBrickEntityDecorator
{
    /// <inheritdoc/>
    public bool AppliesTo(Brick brick)
    {
        float rated = brick.GetStatistic(StatisticIds.POWER_KW);
        return float.IsFinite(rated) && rated != 0f;
    }

    /// <inheritdoc/>
    public void OnSpawn(DataStore store, int entity, Brick brick, in BrickEntityData? persisted)
    {
        float rated = brick.GetStatistic(StatisticIds.POWER_KW);
        if (!float.IsFinite(rated) || rated == 0f)
        {
            store.Remove<PowerNodeComponent>(entity);
            return;
        }

        float demand = MathF.Abs(rated);
        float standby = brick.GetStatistic(StatisticIds.POWER_STANDBY_KW, 0f);
        if (!float.IsFinite(standby) || standby < 0f)
        {
            standby = 0f;
        }

        store.AddOrUpdate(entity, new PowerNodeComponent
        {
            RatedKw = rated,
            StandbyKw = Math.Min(standby, demand),
            Priority = PowerGridPriority.Resolve(brick.StationTag),
            HasFuelGroup = HasFuelGroup(brick),
        });
    }

    private static bool HasFuelGroup(Brick brick)
    {
        if (brick.Entity is not { HasInventory: true } meta)
        {
            return false;
        }

        for (var i = 0; i < meta.SlotGroups.Count; i++)
        {
            if (string.Equals(meta.SlotGroups[i].GroupId, SlotGroups.FUEL, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
