#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Gameplay.Power;
using WaywardBeyond.Recipes;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins the structure-level power bus: nominal balance, standby demand, generator fuel depletion,
/// deterministic brownout shedding, power toggles, telemetry, and node removal cleanup.
/// </summary>
public sealed class PowerGridTests : IDisposable
{
    private const string BIOMASS = "wb:biomass";

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-power-tests-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void NominalBalancePowersEveryConsumerAndPublishesTelemetry()
    {
        BrickDatabase bricks = LoadBricks();
        var brickEntities = new ServerBrickEntityService(bricks);
        var power = new ServerPowerGridSystem();
        var store = new DataStore();
        Uuid structureUuid = CreateStructure(store, 10001);

        int core = Place(brickEntities, store, bricks, structureUuid, new Int3(0, 0, 0), "wb:core");
        int console = Place(brickEntities, store, bricks, structureUuid, new Int3(1, 0, 0), "wb:console");
        Assert.NotEqual(Entity.Null, core);
        Assert.NotEqual(Entity.Null, console);
        Assert.True(store.TryGet(core, out PowerNodeComponent coreNode));
        Assert.True(coreNode.IsProducer);

        power.Tick(0f, store);

        Assert.True(store.TryGet(structureUuid, out int structureEntity));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent grid));
        Assert.Equal(50f, grid.SupplyKw);
        Assert.Equal(1f, grid.DemandKw);
        Assert.Equal(PowerGridStatus.Nominal, grid.Status);
        Assert.Equal(1, grid.ProducerCount);
        Assert.Equal(1, grid.ConsumerCount);

        Assert.True(store.TryGet(console, out MachineStateComponent consoleState));
        Assert.True(consoleState.HasPower);
        Assert.False(consoleState.HasStatusFlag(MachineStatusFlags.NoPower));

        Assert.True(store.TryGet(core, out MachineStateComponent coreState));
        Assert.True(coreState.HasPower);
        Assert.False(coreState.HasStatusFlag(MachineStatusFlags.NoPower));

        Result<GridPowerTelemetry> telemetry = power.GetTelemetry(store, structureUuid);
        Assert.True(telemetry.Success, telemetry.Message);
        Assert.Equal(49f, telemetry.Value.NetKw);
        Assert.Equal(2f, telemetry.Value.LoadPercent);
        Assert.False(telemetry.Value.Brownout);
        Assert.Equal(1, telemetry.Value.ProducerCount);
        Assert.Equal(1, telemetry.Value.ConsumerCount);
    }

    [Fact]
    public void StandbyDemandAppliesWhileIdleAndNominalDemandWhileProcessing()
    {
        BrickDatabase bricks = LoadBricks();
        var brickEntities = new ServerBrickEntityService(bricks);
        var power = new ServerPowerGridSystem();
        var store = new DataStore();
        Uuid structureUuid = CreateStructure(store, 10002);

        Place(brickEntities, store, bricks, structureUuid, new Int3(0, 0, 0), "wb:core");
        int refinery = Place(brickEntities, store, bricks, structureUuid, new Int3(1, 0, 0), "wb:refinery");
        Assert.NotEqual(Entity.Null, refinery);

        power.Tick(0f, store);

        Assert.True(store.TryGet(structureUuid, out int structureEntity));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent idleGrid));
        Assert.Equal(2f, idleGrid.DemandKw);
        Assert.Equal(PowerGridStatus.Nominal, idleGrid.Status);

        Assert.True(store.TryGet(refinery, out MachineStateComponent machine));
        machine.ActiveRecipeId = "wb:smelt_panel";
        store.AddOrUpdate(refinery, machine);
        power.MarkDirty(structureUuid);
        power.Tick(0f, store);

        Assert.True(store.TryGet(structureEntity, out PowerGridComponent activeGrid));
        Assert.Equal(20f, activeGrid.DemandKw);
        Assert.Equal(PowerGridStatus.Nominal, activeGrid.Status);
    }

    [Fact]
    public void GeneratorBurnsFuelThenRevertsToBaselineWhenFuelDepletes()
    {
        BrickDatabase bricks = LoadBricks();
        var brickEntities = new ServerBrickEntityService(bricks);
        var recipes = new FakeRecipeDatabase(
            CreateGeneratorRecipe("wb:burn_biomass", BIOMASS, powerKw: 60f, durationSeconds: 2f));
        var power = new ServerPowerGridSystem(recipes);
        var store = new DataStore();
        Uuid structureUuid = CreateStructure(store, 10003);

        int core = Place(brickEntities, store, bricks, structureUuid, new Int3(0, 0, 0), "wb:core");
        Place(brickEntities, store, bricks, structureUuid, new Int3(1, 0, 0), "wb:console");
        SeedFuel(store, core, BIOMASS, count: 2);
        Assert.Equal(2, CountFuel(store, core, BIOMASS));

        //  Fuel ignition consumes the first unit immediately and raises supply above the 50 kW baseline.
        power.Tick(0f, store);
        Assert.Equal(1, CountFuel(store, core, BIOMASS));

        Assert.True(store.TryGet(structureUuid, out int structureEntity));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent burningGrid));
        Assert.Equal(60f, burningGrid.SupplyKw);
        Assert.Equal(PowerGridStatus.Nominal, burningGrid.Status);

        //  Halfway through the burn the fuel output still applies.
        power.Tick(1f, store);
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent halfwayGrid));
        Assert.Equal(60f, halfwayGrid.SupplyKw);

        //  Past the burn duration the next unit ignites and the fueled rate stays applied.
        power.Tick(1.1f, store);
        Assert.Equal(0, CountFuel(store, core, BIOMASS));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent secondBurnGrid));
        Assert.Equal(60f, secondBurnGrid.SupplyKw);

        //  After the last unit burns away the grid falls back to the starter baseline.
        power.Tick(2.1f, store);
        Assert.Equal(0, CountFuel(store, core, BIOMASS));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent depletedGrid));
        Assert.Equal(50f, depletedGrid.SupplyKw);
        Assert.Equal(PowerGridStatus.Nominal, depletedGrid.Status);
    }

    [Fact]
    public void BrownoutShedsLowestPriorityConsumersAndKeepsLifeSupportAlive()
    {
        BrickDatabase bricks = LoadBricks();
        var brickEntities = new ServerBrickEntityService(bricks);
        var power = new ServerPowerGridSystem();
        var store = new DataStore();
        Uuid structureUuid = CreateStructure(store, 10004);

        Place(brickEntities, store, bricks, structureUuid, new Int3(0, 0, 0), "wb:core");
        int lifeSupport = Place(brickEntities, store, bricks, structureUuid, new Int3(2, 0, 0), "wb:o2_extractor");
        int refinery = Place(brickEntities, store, bricks, structureUuid, new Int3(1, 0, 0), "wb:refinery");
        Assert.NotEqual(Entity.Null, lifeSupport);
        Assert.NotEqual(Entity.Null, refinery);

        //  The refinery is actively processing, so it draws its full 20 kW instead of standby.
        Assert.True(store.TryGet(refinery, out MachineStateComponent processing));
        processing.ActiveRecipeId = "wb:smelt_panel";
        store.AddOrUpdate(refinery, processing);

        power.Tick(0f, store);

        Assert.True(store.TryGet(structureUuid, out int structureEntity));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent grid));
        Assert.Equal(50f, grid.SupplyKw);
        Assert.Equal(60f, grid.DemandKw);
        Assert.Equal(PowerGridStatus.Brownout, grid.Status);
        Assert.True(grid.Telemetry.Brownout);

        Assert.True(store.TryGet(lifeSupport, out MachineStateComponent lifeSupportState));
        Assert.True(lifeSupportState.HasPower);
        Assert.False(lifeSupportState.HasStatusFlag(MachineStatusFlags.NoPower));

        Assert.True(store.TryGet(refinery, out MachineStateComponent refineryState));
        Assert.False(refineryState.HasPower);
        Assert.True(refineryState.HasStatusFlag(MachineStatusFlags.NoPower));

        //  A single life-support node above total supply overloads the grid and sheds every tier.
        Uuid overloadedUuid = CreateStructure(store, 10005);
        Place(brickEntities, store, bricks, overloadedUuid, new Int3(0, 0, 0), "wb:core");
        int heavySupport = Place(brickEntities, store, bricks, overloadedUuid, new Int3(1, 0, 0), "wb:heavy_o2_extractor");
        Assert.NotEqual(Entity.Null, heavySupport);

        power.Tick(0.5f, store);

        Assert.True(store.TryGet(overloadedUuid, out int overloadedEntity));
        Assert.True(store.TryGet(overloadedEntity, out PowerGridComponent overloadedGrid));
        Assert.Equal(60f, overloadedGrid.DemandKw);
        Assert.Equal(PowerGridStatus.Overload, overloadedGrid.Status);
        Assert.False(store.TryGet(heavySupport, out MachineStateComponent heavyState) && heavyState.HasPower);
    }

    [Fact]
    public void TogglingPowerOffImmediatelyRecomputesTheBus()
    {
        BrickDatabase bricks = LoadBricks();
        var brickEntities = new ServerBrickEntityService(bricks);
        var power = new ServerPowerGridSystem();
        var store = new DataStore();
        Uuid structureUuid = CreateStructure(store, 10006);

        int core = Place(brickEntities, store, bricks, structureUuid, new Int3(0, 0, 0), "wb:core");
        int console = Place(brickEntities, store, bricks, structureUuid, new Int3(1, 0, 0), "wb:console");
        power.Tick(0f, store);

        Assert.True(store.TryGet(console, out MachineStateComponent enabledConsole));
        enabledConsole.PowerEnabled = false;
        store.AddOrUpdate(console, enabledConsole);
        power.MarkDirty(structureUuid);
        power.Tick(0f, store);

        Assert.True(store.TryGet(structureUuid, out int structureEntity));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent noDemandGrid));
        Assert.Equal(0f, noDemandGrid.DemandKw);
        Assert.Equal(0, noDemandGrid.ConsumerCount);
        Assert.Equal(PowerGridStatus.Nominal, noDemandGrid.Status);
        Assert.True(store.TryGet(console, out MachineStateComponent offConsole));
        Assert.False(offConsole.HasPower);
        Assert.False(offConsole.HasStatusFlag(MachineStatusFlags.NoPower));

        //  Disabling the generator drops supply to zero and overloads the re-enabled console.
        Assert.True(store.TryGet(core, out MachineStateComponent enabledCore));
        enabledCore.PowerEnabled = false;
        store.AddOrUpdate(core, enabledCore);
        Assert.True(store.TryGet(console, out MachineStateComponent disabledConsole));
        disabledConsole.PowerEnabled = true;
        store.AddOrUpdate(console, disabledConsole);

        power.MarkDirty(structureUuid);
        power.Tick(0f, store);

        Assert.True(store.TryGet(structureEntity, out PowerGridComponent unpoweredGrid));
        Assert.Equal(0f, unpoweredGrid.SupplyKw);
        Assert.Equal(1f, unpoweredGrid.DemandKw);
        Assert.Equal(PowerGridStatus.Overload, unpoweredGrid.Status);
        Assert.True(store.TryGet(console, out MachineStateComponent shedConsole));
        Assert.False(shedConsole.HasPower);
        Assert.True(shedConsole.HasStatusFlag(MachineStatusFlags.NoPower));
    }

    [Fact]
    public void RemovingNodesRecomputesTheBusAndClearsTheStructureSnapshot()
    {
        BrickDatabase bricks = LoadBricks();
        var brickEntities = new ServerBrickEntityService(bricks);
        var power = new ServerPowerGridSystem();
        var store = new DataStore();
        Uuid structureUuid = CreateStructure(store, 10007);

        Place(brickEntities, store, bricks, structureUuid, new Int3(0, 0, 0), "wb:core");
        var consoleCoord = new Int3(1, 0, 0);
        Place(brickEntities, store, bricks, structureUuid, consoleCoord, "wb:console");
        power.Tick(0f, store);

        Assert.True(store.TryGet(structureUuid, out int structureEntity));
        Assert.True(store.TryGet(structureEntity, out PowerGridComponent initialGrid));
        Assert.Equal(1f, initialGrid.DemandKw);

        Assert.True(brickEntities.OnBrickBroken(store, structureUuid, consoleCoord));
        power.Tick(0.5f, store);

        Assert.True(store.TryGet(structureEntity, out PowerGridComponent trimmedGrid));
        Assert.Equal(0f, trimmedGrid.DemandKw);
        Assert.Equal(0, trimmedGrid.ConsumerCount);
        Assert.Equal(PowerGridStatus.Nominal, trimmedGrid.Status);

        Assert.True(brickEntities.OnBrickBroken(store, structureUuid, new Int3(0, 0, 0)));
        power.Tick(0.5f, store);

        Assert.False(store.TryGet(structureEntity, out PowerGridComponent _));
        Assert.False(power.GetTelemetry(store, structureUuid).Success);
        Assert.Equal(0, brickEntities.Count);
    }

    [Fact]
    public void PowerDecoratorAttachesNodesOnlyToRatedBricks()
    {
        BrickDatabase bricks = LoadBricks();
        var brickEntities = new ServerBrickEntityService(bricks);
        var store = new DataStore();
        Uuid structureUuid = CreateStructure(store, 10008);

        int core = Place(brickEntities, store, bricks, structureUuid, new Int3(0, 0, 0), "wb:core");
        int panel = Place(brickEntities, store, bricks, structureUuid, new Int3(1, 0, 0), "wb:panel");

        Assert.True(store.TryGet(core, out PowerNodeComponent coreNode));
        Assert.Equal(50f, coreNode.RatedKw);
        Assert.True(coreNode.HasFuelGroup);
        Assert.Equal(PowerGridPriority.AUXILIARY, coreNode.Priority);
        Assert.Equal(Entity.Null, panel);
        Assert.False(store.TryGet(panel, out PowerNodeComponent _));
    }

    private static Uuid CreateStructure(DataStore store, ulong value)
    {
        Uuid structureUuid = Uuid.FromValue(value);
        int structure = store.Alloc(structureUuid);
        store.AddOrUpdate(structure, new NetworkComponent());
        return structureUuid;
    }

    private static int Place(
        ServerBrickEntityService brickEntities,
        DataStore store,
        BrickDatabase bricks,
        Uuid structureUuid,
        Int3 coord,
        string brickId
    ) {
        return brickEntities.OnBrickPlaced(store, structureUuid, coord, bricks.Id(brickId));
    }

    private static void SeedFuel(DataStore store, int brickEntity, string itemId, int count)
    {
        Assert.True(store.TryGet(brickEntity, out InventoryComponent inventory));
        InventoryComponent updated = inventory.Clone();
        if (count > updated.GetGroupCapacity(SlotGroups.FUEL))
        {
            Assert.True(updated.ResizeGroup(SlotGroups.FUEL, count));
        }

        Assert.True(updated.Add(new ItemData(itemId, count, 100), SlotGroups.FUEL));
        store.AddOrUpdate(brickEntity, updated);
    }

    private static int CountFuel(DataStore store, int brickEntity, string itemId)
    {
        if (!store.TryGet(brickEntity, out InventoryComponent inventory))
        {
            return 0;
        }

        var total = 0;
        int capacity = inventory.GetGroupCapacity(SlotGroups.FUEL);
        for (var i = 0; i < capacity; i++)
        {
            ItemData item = inventory.Get(new SlotId(SlotGroups.FUEL, i));
            if (string.Equals(item.ID, itemId, StringComparison.Ordinal))
            {
                total += item.Count;
            }
        }

        return total;
    }

    private static Recipe CreateGeneratorRecipe(string id, string itemId, float powerKw, float durationSeconds)
    {
        return new Recipe(
            id,
            ServerPowerGridSystem.GENERATOR_RECIPE_TAG,
            RecipeMode.Auto,
            [new RecipeIngredient(itemId, itemId, null, 1, null, [itemId], null)],
            [],
            durationSeconds,
            powerKw,
            category: string.Empty,
            priority: 0);
    }

    private BrickDatabase LoadBricks()
    {
        string bricksDir = Path.Combine(_tempRoot, "bricks");
        string tagsDir = Path.Combine(_tempRoot, "lang", "tags");
        Directory.CreateDirectory(bricksDir);
        Directory.CreateDirectory(tagsDir);

        File.WriteAllText(
            Path.Combine(tagsDir, "bricks.toml"),
            """
            "Language" = ""
            [Tags]
            "station.core" = ["wb:core"]
            "station.console" = ["wb:console"]
            "station.o2_extractor" = ["wb:o2_extractor"]
            "station.refinery" = ["wb:refinery"]
            "station.heavy_o2_extractor" = ["wb:heavy_o2_extractor"]
            """
        );

        File.WriteAllText(
            Path.Combine(bricksDir, "machines.toml"),
            """
            [[Bricks]]
            ID = "wb:core"
            [Bricks.Statistics]
            "wb:power_kw" = 50.0
            [[Bricks.Entity.SlotGroups]]
            ID = "wb:fuel"

            [[Bricks]]
            ID = "wb:console"
            [Bricks.Statistics]
            "wb:power_kw" = -1.0

            [[Bricks]]
            ID = "wb:o2_extractor"
            [Bricks.Statistics]
            "wb:power_kw" = -40.0

            [[Bricks]]
            ID = "wb:heavy_o2_extractor"
            [Bricks.Statistics]
            "wb:power_kw" = -60.0

            [[Bricks]]
            ID = "wb:refinery"
            [Bricks.Statistics]
            "wb:power_kw" = -20.0
            "wb:power_standby_kw" = 2.0

            [[Bricks]]
            ID = "wb:panel"
            """
        );

        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(_tempRoot));
        IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
        var parseService = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);
        return new BrickDatabase(NullLogger<BrickDatabase>.Instance, parseService, vfs, tags);
    }

    private sealed class FakeRecipeDatabase(params Recipe[] recipes) : IRecipeDatabase
    {
        public IReadOnlyList<Recipe> All => recipes;

        public Result<Recipe> Get(string id)
        {
            for (var i = 0; i < recipes.Length; i++)
            {
                if (string.Equals(recipes[i].ID, id, StringComparison.Ordinal))
                {
                    return Result<Recipe>.FromSuccess(recipes[i]);
                }
            }

            return Result<Recipe>.FromFailure($"Unknown recipe \"{id}\".");
        }

        public IReadOnlyList<Recipe> GetByTargetTag(string targetTag)
        {
            return [.. recipes.Where(recipe => string.Equals(recipe.TargetTag, targetTag, StringComparison.Ordinal))];
        }

        public Result<Recipe> FindAutoMatch(string targetTag, ReadOnlySpan<ItemData> inputs)
        {
            for (var i = 0; i < recipes.Length; i++)
            {
                Recipe recipe = recipes[i];
                if (recipe.Mode != RecipeMode.Auto
                    || !string.Equals(recipe.TargetTag, targetTag, StringComparison.Ordinal))
                {
                    continue;
                }

                if (MatchesInputs(in recipe, inputs))
                {
                    return Result<Recipe>.FromSuccess(recipe);
                }
            }

            return Result<Recipe>.FromFailure("No matching automatic recipe found");
        }

        public bool IsValidInputForStation(string targetTag, string itemId)
        {
            return recipes.Any(recipe =>
                string.Equals(recipe.TargetTag, targetTag, StringComparison.Ordinal)
                && recipe.InputSpan.ToArray().Any(ingredient => ingredient.Matches(itemId)));
        }

        private static bool MatchesInputs(in Recipe recipe, ReadOnlySpan<ItemData> inputs)
        {
            ReadOnlySpan<RecipeIngredient> ingredients = recipe.InputSpan;
            for (var i = 0; i < ingredients.Length; i++)
            {
                ref readonly RecipeIngredient ingredient = ref ingredients[i];
                var available = 0;
                for (var s = 0; s < inputs.Length; s++)
                {
                    if (ingredient.Matches(inputs[s].ID))
                    {
                        available += inputs[s].Count;
                    }
                }

                if (available < ingredient.Count)
                {
                    return false;
                }
            }

            return ingredients.Length > 0;
        }
    }
}
