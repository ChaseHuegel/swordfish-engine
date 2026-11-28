using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Power;
using WaywardBeyond.Recipes;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-authoritative structure-level power bus. Sums enabled generation and demand across the
/// brick-entities anchored to each voxel structure, burns generator fuel recipes, sheds consumers by
/// deterministic priority during a brownout, and publishes a <see cref="PowerGridComponent"/> snapshot
/// on the structure entity. Evaluation is event-driven and low-frequency so idle structures cost almost
/// no CPU.
/// </summary>
public sealed class ServerPowerGridSystem : IServerWorldSystem
{
    /// <summary>Seconds between full structure scans when no invalidation arrives.</summary>
    public const float EVALUATION_INTERVAL_SECONDS = 0.5f;

    /// <summary>Recipe target tag consumed by fuel-burning generator brick-entities.</summary>
    public const string GENERATOR_RECIPE_TAG = "station.generator";

    private const float POWER_EPSILON = 0.0001f;

    private static readonly Comparison<PowerNodeSnapshot> _nodeComparison = CompareNodes;

    private readonly IRecipeDatabase? _recipes;
    private readonly ILogger<ServerPowerGridSystem>? _logger;

    private readonly Dictionary<int, FuelBurnState> _burns = [];
    private readonly HashSet<Uuid> _structures = [];
    private readonly HashSet<Uuid> _dirtyStructures = [];
    private readonly HashSet<int> _liveNodeEntities = [];
    private readonly List<PowerNodeSnapshot> _nodes = [];
    private readonly List<int> _consumerIndices = [];
    private readonly List<int> _burnScratch = [];
    private readonly List<Uuid> _structureScratch = [];
    private readonly List<Uuid> _staleStructures = [];
    private readonly List<ItemData> _fuelScratch = [];

    private float _sinceEvaluation = EVALUATION_INTERVAL_SECONDS;

    /// <summary>Constructs the per-world power bus system.</summary>
    public ServerPowerGridSystem(
        IRecipeDatabase? recipes = null,
        ILogger<ServerPowerGridSystem>? logger = null
    ) {
        _recipes = recipes;
        _logger = logger;
    }

    /// <summary>Marks <paramref name="structureUuid"/> for immediate re-evaluation on the next tick.</summary>
    public void MarkDirty(Uuid structureUuid)
    {
        if (structureUuid != Uuid.Null)
        {
            _dirtyStructures.Add(structureUuid);
        }
    }

    /// <summary>Gets the last published telemetry for <paramref name="structureUuid"/>.</summary>
    public Result<GridPowerTelemetry> GetTelemetry(DataStore store, Uuid structureUuid)
    {
        if (structureUuid == Uuid.Null)
        {
            return Result<GridPowerTelemetry>.FromFailure("Structure UUID is null.");
        }

        if (!store.TryGet(structureUuid, out int structureEntity))
        {
            return Result<GridPowerTelemetry>.FromFailure("Structure entity is not present.");
        }

        if (!store.TryGet(structureEntity, out PowerGridComponent grid))
        {
            return Result<GridPowerTelemetry>.FromFailure("Structure does not host a power grid.");
        }

        return Result<GridPowerTelemetry>.FromSuccess(grid.Telemetry);
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        float dt = float.IsFinite(delta) && delta > 0f ? delta : 0f;
        AdvanceBurns(dt, store);

        _sinceEvaluation += dt;
        bool evaluate = _dirtyStructures.Count > 0 || _sinceEvaluation >= EVALUATION_INTERVAL_SECONDS;
        if (!evaluate)
        {
            return;
        }

        _sinceEvaluation = 0f;
        _dirtyStructures.Clear();
        Evaluate(store);
    }

    private void Evaluate(DataStore store)
    {
        _nodes.Clear();
        CollectAction collect = new() { Owner = this };
        store.Query<PowerNodeComponent, BrickAnchorComponent, CollectAction>(0f, ref collect);
        _nodes.Sort(_nodeComparison);

        _liveNodeEntities.Clear();
        for (var i = 0; i < _nodes.Count; i++)
        {
            _liveNodeEntities.Add(_nodes[i].Entity);
        }

        DropOrphanedBurns();

        _structureScratch.Clear();
        var start = 0;
        while (start < _nodes.Count)
        {
            Uuid structureUuid = _nodes[start].StructureUuid;
            var end = start + 1;
            while (end < _nodes.Count && _nodes[end].StructureUuid == structureUuid)
            {
                end++;
            }

            EvaluateStructure(store, structureUuid, start, end);
            _structureScratch.Add(structureUuid);
            start = end;
        }

        _staleStructures.Clear();
        foreach (Uuid tracked in _structures)
        {
            if (!_structureScratch.Contains(tracked))
            {
                _staleStructures.Add(tracked);
            }
        }

        for (var i = 0; i < _staleStructures.Count; i++)
        {
            ClearStructure(store, _staleStructures[i]);
        }

        for (var i = 0; i < _structureScratch.Count; i++)
        {
            _structures.Add(_structureScratch[i]);
        }
    }

    private void EvaluateStructure(DataStore store, Uuid structureUuid, int start, int end)
    {
        float supply = 0f;
        float demand = 0f;
        var producerCount = 0;
        var consumerCount = 0;

        _consumerIndices.Clear();

        for (var i = start; i < end; i++)
        {
            PowerNodeSnapshot node = _nodes[i];
            if (node.RatedKw > 0f)
            {
                if (!node.PowerEnabled)
                {
                    ApplyMachinePower(store, node.Entity, PowerAssignment.Disabled);
                    continue;
                }

                if (!_burns.ContainsKey(node.Entity))
                {
                    TryStartBurn(store, in node);
                }

                float output = node.RatedKw;
                if (_burns.TryGetValue(node.Entity, out FuelBurnState? burn) && burn.OutputKw > output)
                {
                    output = burn.OutputKw;
                }

                supply += output;
                producerCount++;
                ApplyMachinePower(store, node.Entity, PowerAssignment.Powered);
                continue;
            }

            if (!node.PowerEnabled)
            {
                ApplyMachinePower(store, node.Entity, PowerAssignment.Disabled);
                continue;
            }

            demand += ResolveDemand(node);
            consumerCount++;
            _consumerIndices.Add(i);
        }

        PowerGridStatus status = AllocateDemand(store, supply, demand);
        PublishStructure(store, structureUuid, supply, demand, status, producerCount, consumerCount);
    }

    private PowerGridStatus AllocateDemand(DataStore store, float supply, float demand)
    {
        PowerGridStatus status = demand <= supply + POWER_EPSILON
            ? PowerGridStatus.Nominal
            : PowerGridStatus.Overload;

        float allocated = 0f;
        var blocked = false;
        var topTierPowered = false;
        var firstTier = true;
        var index = 0;

        while (index < _consumerIndices.Count)
        {
            byte tier = _nodes[_consumerIndices[index]].Priority;
            var tierEnd = index;
            float tierDemand = 0f;
            while (tierEnd < _consumerIndices.Count && _nodes[_consumerIndices[tierEnd]].Priority == tier)
            {
                tierDemand += ResolveDemand(_nodes[_consumerIndices[tierEnd]]);
                tierEnd++;
            }

            bool powered = !blocked && allocated + tierDemand <= supply + POWER_EPSILON;
            if (powered)
            {
                allocated += tierDemand;
                if (firstTier)
                {
                    topTierPowered = true;
                }
            }
            else
            {
                blocked = true;
            }

            for (var i = index; i < tierEnd; i++)
            {
                int consumerEntity = _nodes[_consumerIndices[i]].Entity;
                ApplyMachinePower(store, consumerEntity, powered ? PowerAssignment.Powered : PowerAssignment.Shed);
            }

            firstTier = false;
            index = tierEnd;
        }

        if (status == PowerGridStatus.Overload && topTierPowered)
        {
            status = PowerGridStatus.Brownout;
        }

        return status;
    }

    private void PublishStructure(
        DataStore store,
        Uuid structureUuid,
        float supply,
        float demand,
        PowerGridStatus status,
        int producerCount,
        int consumerCount
    ) {
        if (!store.TryGet(structureUuid, out int structureEntity))
        {
            return;
        }

        var grid = new PowerGridComponent
        {
            SupplyKw = supply,
            DemandKw = demand,
            Status = status,
            ProducerCount = producerCount,
            ConsumerCount = consumerCount,
        };

        if (!store.TryGet(structureEntity, out PowerGridComponent existing) || !existing.EqualsState(in grid))
        {
            store.AddOrUpdate(structureEntity, grid);
        }
    }

    private void ClearStructure(DataStore store, Uuid structureUuid)
    {
        _structures.Remove(structureUuid);

        if (_burns.Count > 0)
        {
            _burnScratch.Clear();
            foreach ((int entity, FuelBurnState burn) in _burns)
            {
                if (burn.StructureUuid == structureUuid)
                {
                    _burnScratch.Add(entity);
                }
            }

            for (var i = 0; i < _burnScratch.Count; i++)
            {
                _burns.Remove(_burnScratch[i]);
            }
        }

        if (store.TryGet(structureUuid, out int structureEntity))
        {
            store.Remove<PowerGridComponent>(structureEntity);
        }
    }

    private void AdvanceBurns(float delta, DataStore store)
    {
        if (_burns.Count == 0 || delta <= 0f)
        {
            return;
        }

        _burnScratch.Clear();
        foreach ((int entity, FuelBurnState burn) in _burns)
        {
            burn.Remaining -= delta;
            if (burn.Remaining <= 0f)
            {
                _burnScratch.Add(entity);
            }
        }

        for (var i = 0; i < _burnScratch.Count; i++)
        {
            int entity = _burnScratch[i];
            if (!_burns.Remove(entity, out FuelBurnState? completed))
            {
                continue;
            }

            _logger?.LogDebug(
                "Generator {entity} finished burning recipe \"{recipe}\" on structure {structure}.",
                entity, completed.RecipeId, completed.StructureUuid);
            MarkDirty(completed.StructureUuid);

            if (TryStartBurn(store, entity, completed.StructureUuid))
            {
                MarkDirty(completed.StructureUuid);
            }
        }
    }

    private void DropOrphanedBurns()
    {
        if (_burns.Count == 0)
        {
            return;
        }

        _burnScratch.Clear();
        foreach (int entity in _burns.Keys)
        {
            if (!_liveNodeEntities.Contains(entity))
            {
                _burnScratch.Add(entity);
            }
        }

        for (var i = 0; i < _burnScratch.Count; i++)
        {
            _burns.Remove(_burnScratch[i]);
        }
    }

    private bool TryStartBurn(DataStore store, in PowerNodeSnapshot node)
    {
        return TryStartBurn(store, node.Entity, node.StructureUuid);
    }

    private bool TryStartBurn(DataStore store, int entity, Uuid structureUuid)
    {
        if (_recipes == null
            || !store.TryGet(entity, out PowerNodeComponent power)
            || !power.IsProducer
            || !power.HasFuelGroup
            || !store.TryGet(entity, out MachineStateComponent machine)
            || !machine.PowerEnabled
            || !store.TryGet(entity, out InventoryComponent inventory)
            || !inventory.HasGroup(SlotGroups.FUEL))
        {
            return false;
        }

        int capacity = inventory.GetGroupCapacity(SlotGroups.FUEL);
        _fuelScratch.Clear();
        for (var i = 0; i < capacity; i++)
        {
            ItemData item = inventory.Get(new SlotId(SlotGroups.FUEL, i));
            if (!item.IsEmpty())
            {
                _fuelScratch.Add(item);
            }
        }

        if (_fuelScratch.Count == 0)
        {
            return false;
        }

        Result<Recipe> match = _recipes.FindAutoMatch(GENERATOR_RECIPE_TAG, CollectionsMarshal.AsSpan(_fuelScratch));
        if (!match.Success)
        {
            return false;
        }

        Recipe recipe = match.Value;
        if (!(recipe.PowerKw > 0f) || !(recipe.DurationSeconds > 0f))
        {
            return false;
        }

        if (!ConsumeFuel(store, entity, in inventory, in recipe))
        {
            return false;
        }

        _burns[entity] = new FuelBurnState
        {
            StructureUuid = structureUuid,
            RecipeId = recipe.ID,
            Remaining = recipe.DurationSeconds,
            OutputKw = recipe.PowerKw,
        };

        _logger?.LogDebug(
            "Generator {entity} started burning recipe \"{recipe}\" on structure {structure} for {duration}s at {power}kW.",
            entity, recipe.ID, structureUuid, recipe.DurationSeconds, recipe.PowerKw);
        return true;
    }

    private static bool ConsumeFuel(DataStore store, int entity, in InventoryComponent inventory, in Recipe recipe)
    {
        InventoryComponent updated = inventory.Clone();
        int capacity = updated.GetGroupCapacity(SlotGroups.FUEL);
        ReadOnlySpan<RecipeIngredient> ingredients = recipe.InputSpan;

        for (var i = 0; i < ingredients.Length; i++)
        {
            ref readonly RecipeIngredient ingredient = ref ingredients[i];
            int needed = ingredient.Count;
            for (var slot = 0; slot < capacity && needed > 0; slot++)
            {
                var slotId = new SlotId(SlotGroups.FUEL, slot);
                ItemData stack = updated.Get(in slotId);
                if (stack.IsEmpty() || !ingredient.Matches(stack.ID))
                {
                    continue;
                }

                int take = Math.Min(needed, stack.Count);
                Result<ItemData> removed = updated.Remove(in slotId, take);
                if (!removed.Success)
                {
                    return false;
                }

                needed -= take;
            }

            if (needed > 0)
            {
                return false;
            }
        }

        store.AddOrUpdate(entity, updated);
        return true;
    }

    private static float ResolveDemand(PowerNodeSnapshot node)
    {
        float rated = node.RatedKw < 0f ? -node.RatedKw : 0f;
        if (node.HasActiveRecipe)
        {
            return rated;
        }

        return node.StandbyKw > 0f ? node.StandbyKw : rated;
    }

    private static void ApplyMachinePower(DataStore store, int entity, PowerAssignment assignment)
    {
        if (!store.TryGet(entity, out MachineStateComponent machine))
        {
            return;
        }

        bool hasPower = assignment == PowerAssignment.Powered;
        bool hasNoPower = assignment == PowerAssignment.Shed;
        if (machine.HasPower == hasPower && machine.HasStatusFlag(MachineStatusFlags.NoPower) == hasNoPower)
        {
            return;
        }

        machine.HasPower = hasPower;
        machine.SetStatusFlag(MachineStatusFlags.NoPower, hasNoPower);
        store.AddOrUpdate(entity, machine);
    }

    private static int CompareNodes(PowerNodeSnapshot left, PowerNodeSnapshot right)
    {
        int compare = left.StructureUuid.ToValue().CompareTo(right.StructureUuid.ToValue());
        if (compare != 0)
        {
            return compare;
        }

        compare = left.Priority.CompareTo(right.Priority);
        if (compare != 0)
        {
            return compare;
        }

        compare = left.Coord.X.CompareTo(right.Coord.X);
        if (compare != 0)
        {
            return compare;
        }

        compare = left.Coord.Y.CompareTo(right.Coord.Y);
        if (compare != 0)
        {
            return compare;
        }

        compare = left.Coord.Z.CompareTo(right.Coord.Z);
        if (compare != 0)
        {
            return compare;
        }

        return left.EntityUuid.ToValue().CompareTo(right.EntityUuid.ToValue());
    }

    private readonly struct PowerNodeSnapshot
    {
        public PowerNodeSnapshot(
            int entity,
            Uuid entityUuid,
            Uuid structureUuid,
            Int3 coord,
            in PowerNodeComponent node,
            bool powerEnabled,
            bool hasActiveRecipe
        ) {
            Entity = entity;
            EntityUuid = entityUuid;
            StructureUuid = structureUuid;
            Coord = coord;
            RatedKw = node.RatedKw;
            StandbyKw = node.StandbyKw;
            Priority = node.Priority;
            PowerEnabled = powerEnabled;
            HasActiveRecipe = hasActiveRecipe;
        }

        public int Entity { get; }
        public Uuid EntityUuid { get; }
        public Uuid StructureUuid { get; }
        public Int3 Coord { get; }
        public float RatedKw { get; }
        public float StandbyKw { get; }
        public byte Priority { get; }
        public bool PowerEnabled { get; }
        public bool HasActiveRecipe { get; }
    }

    private enum PowerAssignment : byte
    {
        Powered,
        Shed,
        Disabled,
    }

    private sealed class FuelBurnState
    {
        public Uuid StructureUuid;
        public string RecipeId = string.Empty;
        public float Remaining;
        public float OutputKw;
    }

    private struct CollectAction : IForEach<PowerNodeComponent, BrickAnchorComponent>
    {
        public ServerPowerGridSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PowerNodeComponent node, in BrickAnchorComponent anchor)
        {
            bool powerEnabled = true;
            bool hasActiveRecipe = false;
            if (store.TryGet(entity, out MachineStateComponent machine))
            {
                powerEnabled = machine.PowerEnabled;
                hasActiveRecipe = !string.IsNullOrEmpty(machine.ActiveRecipeId);
            }

            Owner._nodes.Add(new PowerNodeSnapshot(
                entity,
                store.GetUuid(entity),
                anchor.VoxelEntityUuid,
                anchor.VoxelCoord,
                in node,
                powerEnabled,
                hasActiveRecipe));
        }
    }
}
