using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Maintains the server-authoritative <see cref="LaserBeamComponent"/> on player mirrors holding primary
/// fire with a mining tool equipped so remote clients can render active beams and impact sparks.
/// </summary>
public sealed class ServerLaserBeamSystem(
    IServerWorldPhysics physics,
    IItemDatabase itemDatabase,
    SimulationStep simulationStep
) : IServerWorldSystem
{
    private const float TARGET_DELTA_SQ_THRESHOLD = 0.0004f;
    private const float NORMAL_DELTA_SQ_THRESHOLD = 0.001f;

    private readonly IServerWorldPhysics _physics = physics;
    private readonly IItemDatabase _itemDatabase = itemDatabase;
    private readonly SimulationStep _simulationStep = simulationStep;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        Tick(store, _simulationStep.CurrentSimTick);
    }

    /// <summary>Evaluates active beam state for every player mirror against <paramref name="simTick"/>.</summary>
    public void Tick(DataStore store, uint simTick)
    {
        UpdateAction action = new()
        {
            Owner = this,
            SimTick = simTick,
        };
        store.Query<NetworkComponent, OwnedCharacterComponent, TransformComponent, InputComponent, UpdateAction>(0f, ref action);
    }

    private void UpdatePlayerBeam(
        DataStore store,
        int entity,
        uint simTick,
        in NetworkComponent net,
        in TransformComponent transform,
        in InputComponent latestInput
    ) {
        bool isDead = store.TryGet(entity, out StatisticsComponent stats) && stats.IsDead;
        bool primaryHeld = !isDead && ResolvePrimaryHeld(in net, in latestInput, simTick, _simulationStep.CommandMaxAgeTicks);
        bool holdingLaser = primaryHeld && IsHoldingMiningTool(store, entity);

        if (!holdingLaser)
        {
            if (store.Has(typeof(LaserBeamComponent), entity))
            {
                store.Remove(typeof(LaserBeamComponent), entity);
            }

            return;
        }

        bool hit = AimRay.TryTrace(
            _physics,
            entity,
            in transform,
            CharacterDefaults.DEFAULT_REACH,
            out Vector3 target,
            out Vector3 normal,
            out _
        );

        var candidate = new LaserBeamComponent
        {
            TargetX = target.X,
            TargetY = target.Y,
            TargetZ = target.Z,
            NormalX = normal.X,
            NormalY = normal.Y,
            NormalZ = normal.Z,
            Hit = hit,
        };

        if (store.TryGet(entity, out LaserBeamComponent existing) && !HasChanged(in existing, in candidate))
        {
            return;
        }

        store.AddOrUpdate(entity, candidate);
    }

    private static bool ResolvePrimaryHeld(in NetworkComponent net, in InputComponent latestInput, uint simTick, uint maxAgeTicks)
    {
        //  An empty ring has no staged samples yet, so the live component is the best available source.
        //  Once samples are staged, a stale or future-only ring resolves as idle instead of falling back.
        if (net.StagedInputs == null || net.StagedInputs.Count == 0)
        {
            return latestInput.PrimaryHeld;
        }

        Result<InputComponent> staged = net.StagedInputs.GetCommand(simTick, maxAgeTicks);
        return staged.Success && staged.Value.PrimaryHeld;
    }

    private bool IsHoldingMiningTool(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out EquipmentComponent equipment)
            || !store.TryGet(entity, out InventoryComponent inventory))
        {
            return false;
        }

        ItemData itemData = inventory.Get(new SlotId(SlotGroups.HOTBAR, equipment.ActiveInventorySlot));
        if (itemData.IsEmpty())
        {
            return false;
        }

        Result<Item> itemResult = _itemDatabase.Get(itemData.ID);
        return itemResult.Success && itemResult.Value.Tool?.Type == ToolType.Mining;
    }

    private static bool HasChanged(in LaserBeamComponent current, in LaserBeamComponent next)
    {
        if (current.Hit != next.Hit)
        {
            return true;
        }

        if (Vector3.DistanceSquared(current.Target, next.Target) > TARGET_DELTA_SQ_THRESHOLD)
        {
            return true;
        }

        return Vector3.DistanceSquared(current.Normal, next.Normal) > NORMAL_DELTA_SQ_THRESHOLD;
    }

    private struct UpdateAction : IForEach<NetworkComponent, OwnedCharacterComponent, TransformComponent, InputComponent>
    {
        public ServerLaserBeamSystem Owner;
        public uint SimTick;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            in NetworkComponent net,
            in OwnedCharacterComponent owned,
            in TransformComponent transform,
            in InputComponent input
        ) {
            Owner.UpdatePlayerBeam(store, entity, SimTick, in net, in transform, in input);
        }
    }
}
