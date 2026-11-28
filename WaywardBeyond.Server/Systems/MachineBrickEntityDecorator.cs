using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>Built-in server decorator that provisions <see cref="MachineStateComponent"/> for station, powered, or toggleable bricks.</summary>
public sealed class MachineBrickEntityDecorator : IServerBrickEntityDecorator
{
    /// <inheritdoc/>
    public bool AppliesTo(Brick brick)
    {
        return brick.StationTag.Length > 0
            || brick.Toggleable
            || brick.GetStatistic(StatisticIds.POWER_KW) != 0f;
    }

    /// <inheritdoc/>
    public void OnSpawn(DataStore store, int entity, Brick brick, in BrickEntityData? persisted)
    {
        if (!store.TryGet(entity, out ContainerAccessComponent _))
        {
            store.AddOrUpdate(entity, new ContainerAccessComponent
            {
                MaxReach = CharacterDefaults.DEFAULT_REACH,
            });
        }

        float progress = persisted != null && float.IsFinite(persisted.Value.Progress) && persisted.Value.Progress > 0f
            ? persisted.Value.Progress
            : 0f;
        float duration = persisted != null && float.IsFinite(persisted.Value.Duration) && persisted.Value.Duration > 0f
            ? persisted.Value.Duration
            : 0f;
        bool powerEnabled = persisted?.PowerEnabled ?? brick.DefaultPowerEnabled;
        byte statusFlags = persisted != null
            ? (byte)(persisted.Value.StatusFlags & (byte)MachineStatusFlags.PersistentMask)
            : (brick.DefaultOpen ? (byte)MachineStatusFlags.Open : (byte)MachineStatusFlags.None);

        string stationTag = persisted != null && !string.IsNullOrEmpty(persisted.Value.StationTag)
            ? persisted.Value.StationTag
            : brick.StationTag;

        var machineState = new MachineStateComponent(
            _StationTag: stationTag,
            _ActiveRecipeId: persisted?.ActiveRecipeId,
            _Progress: progress,
            _Duration: duration,
            _PowerEnabled: powerEnabled,
            _HasPower: false,
            _StatusFlags: statusFlags);
        store.AddOrUpdate(entity, machineState);
    }
}
