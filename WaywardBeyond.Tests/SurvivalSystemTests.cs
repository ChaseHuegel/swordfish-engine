#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Gameplay.Survival;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Interactions;
using WaywardBeyond.Server.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Pins Adventure Mode oxygen, suffocation, Creative bypass, buff regen and expiry, consumables, and the damage path.</summary>
public class SurvivalSystemTests
{
    private const string MEDKIT = "wb:medkit";
    private const string O2_CANISTER = "wb:o2_canister";
    private const string RATION = "wb:ration";
    private const string WELL_FED = "wb:well_fed";

    [Fact]
    public void AdventureVacuumDrainsOxygenAtConfiguredRate()
    {
        using var harness = new SurvivalHarness(GameMode.Adventure, oxygen: 100f);

        harness.Tick(1f);

        Assert.Equal(98f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);
        Assert.Equal(100f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
    }

    [Fact]
    public void AdventurePressurizedRefillsOxygenUpToMaximum()
    {
        using var harness = new SurvivalHarness(GameMode.Adventure, oxygen: 20f, pressurized: true);

        harness.Tick(1f);
        Assert.Equal(40f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);

        harness.Tick(10f);
        Assert.Equal(100f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);
    }

    [Fact]
    public void ZeroOxygenAppliesSuffocationDamageAndRecordsCause()
    {
        using var harness = new SurvivalHarness(GameMode.Adventure, health: 100f, oxygen: 0f);

        harness.Tick(1f);

        Assert.Equal(95f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.True(harness.Store.TryGet(harness.Player, out DamageStateComponent damage));
        Assert.Equal(DamageSource.Suffocation, damage.LastSource);
        Assert.Equal(5f, damage.LastAmount, precision: 3);
        Assert.Equal(DamageSource.None, damage.DeathCause);
    }

    [Fact]
    public void SuffocationRecordsDeathCauseWhenHealthReachesZero()
    {
        using var harness = new SurvivalHarness(GameMode.Adventure, health: 3f, oxygen: 0f);

        harness.Tick(1f);

        Assert.Equal(0f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.True(harness.Store.TryGet(harness.Player, out DamageStateComponent damage));
        Assert.Equal(DamageSource.Suffocation, damage.DeathCause);
    }

    [Fact]
    public void CreativeModeBypassesDrainAndKeepsVitalsFull()
    {
        using var harness = new SurvivalHarness(GameMode.Creative, health: 40f, oxygen: 40f);

        harness.Tick(1f);

        Assert.Equal(100f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(100f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);
        Assert.False(harness.Store.TryGet(harness.Player, out DamageStateComponent _));
    }

    [Fact]
    public void ContinuousWritesWaitForTheConfiguredCadence()
    {
        using var harness = new SurvivalHarness(GameMode.Adventure, oxygen: 100f);

        harness.Tick(0.1f);
        Assert.Equal(100f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);

        harness.Tick(0.2f);
        Assert.Equal(99.4f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);
    }

    [Fact]
    public void ActiveBuffRegenHealsHealthAndExpiresAtItsTick()
    {
        Buff wellFed = CreateBuff(WELL_FED, healthPerSecond: 1f, oxygenPerSecond: 0.5f);
        using var harness = new SurvivalHarness(GameMode.Adventure, health: 50f, oxygen: 50f, pressurized: true);
        harness.Buffs.Add(wellFed);
        harness.Step.AlignTo(100);
        harness.SetBuffExpiry(WELL_FED, 102);

        harness.Tick(1f);
        Assert.Equal(51f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(70.5f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);
        Assert.True(harness.GetStats().TryGetBuffExpiresTick(WELL_FED, out _));

        harness.Tick(1f);
        Assert.Equal(51f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.False(harness.GetStats().TryGetBuffExpiresTick(WELL_FED, out _));
    }

    [Fact]
    public void ExpiredBuffEntriesAreRemovedFromStatistics()
    {
        Buff wellFed = CreateBuff(WELL_FED, healthPerSecond: 1f, oxygenPerSecond: 0f);
        using var harness = new SurvivalHarness(GameMode.Adventure, pressurized: true);
        harness.Buffs.Add(wellFed);
        harness.Step.AlignTo(500);
        harness.SetBuffExpiry(WELL_FED, 500);

        harness.Tick(1f);

        Assert.False(harness.GetStats().TryGetBuffExpiresTick(WELL_FED, out _));
    }

    [Fact]
    public void ReadyConsumableCooldownsAreRemovedFromStatistics()
    {
        using var harness = new SurvivalHarness(GameMode.Adventure, pressurized: true);
        string cooldownKey = StatisticIds.Cooldown(MEDKIT.AsSpan());
        harness.Step.AlignTo(500);
        harness.SetStatistic(cooldownKey, 500L);

        harness.Tick(1f);

        Assert.False(harness.GetStats().TryGetValue(cooldownKey, out _));
    }

    [Fact]
    public void ItemDatabaseParsesConsumableBlock()
    {
        using TempItemAssets assets = new("""
            [[Items]]
            ID = "wb:medkit"
            Name = "item.medkit"
            MaxStack = 5
            [Items.Consumable]
            heal_hp = 40.0
            restore_o2 = 10.0
            grant_buff_id = "wb:well_fed"
            cooldown_seconds = 8.0
            """);

        ItemDatabase database = assets.LoadItems();
        Result<Item> result = database.Get(MEDKIT);

        Assert.True(result.Success);
        Assert.True(result.Value.Consumable.HasValue);
        ConsumableDefinition consumable = result.Value.Consumable.Value;
        Assert.Equal(40f, consumable.HealHp);
        Assert.Equal(10f, consumable.RestoreO2);
        Assert.Equal(WELL_FED, consumable.GrantBuffID);
        Assert.Equal(8f, consumable.CooldownSeconds);
    }

    [Fact]
    public void ConsumingAMedkitRestoresHealthAndConsumesOneItem()
    {
        var harness = new ConsumeHarness(GameMode.Adventure, health: 50f);
        harness.Give(MEDKIT, count: 2, new ConsumableDefinition { HealHp = 40f });

        harness.Consume(MEDKIT);

        Assert.Equal(90f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(1, harness.GetHotbarCount(MEDKIT));
    }

    [Fact]
    public void ConsumingAtFullHealthDoesNotWasteTheItem()
    {
        var harness = new ConsumeHarness(GameMode.Adventure, health: 100f);
        harness.Give(MEDKIT, count: 2, new ConsumableDefinition { HealHp = 40f });

        harness.Consume(MEDKIT);

        Assert.Equal(100f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(2, harness.GetHotbarCount(MEDKIT));
    }

    [Fact]
    public void ConsumableCanBeUsedInCreativeWithoutApplyingDamageRules()
    {
        //  Creative bypasses survival drain, but a consumable heal is still a valid request. The server
        //  handler only claims Adventure Mode, so the item is untouched.
        var harness = new ConsumeHarness(GameMode.Creative, health: 50f);
        harness.Give(MEDKIT, count: 2, new ConsumableDefinition { HealHp = 40f });

        harness.Consume(MEDKIT);

        Assert.Equal(50f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(2, harness.GetHotbarCount(MEDKIT));
    }

    [Fact]
    public void OxygenCanisterRestoresOxygen()
    {
        var harness = new ConsumeHarness(GameMode.Adventure, oxygen: 20f);
        harness.Give(O2_CANISTER, count: 1, new ConsumableDefinition { RestoreO2 = 50f });

        harness.Consume(O2_CANISTER);

        Assert.Equal(70f, harness.GetFixed(StatisticIds.OXYGEN), precision: 3);
        Assert.Equal(0, harness.GetHotbarCount(O2_CANISTER));
    }

    [Fact]
    public void RationGrantsItsBuffAndCooldownBlocksImmediateRepeatUse()
    {
        var harness = new ConsumeHarness(GameMode.Adventure, health: 80f);
        harness.Buffs.Add(CreateBuff(WELL_FED, healthPerSecond: 1f, oxygenPerSecond: 0f));
        harness.Give(RATION, count: 2, new ConsumableDefinition
        {
            HealHp = 15f,
            GrantBuffID = WELL_FED,
            CooldownSeconds = 5f,
        });

        harness.Consume(RATION);

        Assert.Equal(95f, harness.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(1, harness.GetHotbarCount(RATION));
        Assert.True(harness.Stats.TryGetBuffExpiresTick(WELL_FED, out long expiry));
        Assert.True(expiry > harness.SimTick);

        harness.Consume(RATION);

        Assert.Equal(1, harness.GetHotbarCount(RATION));
    }

    [Fact]
    public void BuffOnlyConsumableIsNotWastedWhenAnIgnorePolicyBuffIsActive()
    {
        var harness = new ConsumeHarness(GameMode.Adventure);
        harness.Buffs.Add(CreateBuff(WELL_FED, healthPerSecond: 1f, oxygenPerSecond: 0f, BuffStackPolicy.Ignore));
        harness.Give(RATION, count: 2, new ConsumableDefinition { GrantBuffID = WELL_FED });
        harness.SetBuffExpiry(WELL_FED, harness.SimTick + 100);

        harness.Consume(RATION);

        Assert.Equal(2, harness.GetHotbarCount(RATION));
    }

    [Fact]
    public void DamageServiceClampsAndRecordsTheFatalSource()
    {
        var store = new DataStore();
        int player = store.Alloc();
        store.AddOrUpdate(player, new GameModeComponent(GameMode.Adventure));
        var stats = new StatisticsComponent();
        stats.SetFixed(StatisticIds.HEALTH_MAX, 100f);
        stats.SetFixed(StatisticIds.HEALTH, 100f);
        store.AddOrUpdate(player, stats);

        DamageResult first = DamageService.ApplyDamage(store, player, 60f, DamageSource.Kinetic, simTick: 12);
        Assert.Equal(60f, first.Applied, precision: 3);
        Assert.False(first.Fatal);

        DamageResult second = DamageService.ApplyDamage(store, player, 60f, DamageSource.Suffocation, simTick: 13);
        Assert.Equal(40f, second.Applied, precision: 3);
        Assert.True(second.Fatal);

        Assert.True(store.TryGet(player, out StatisticsComponent damaged));
        Assert.Equal(0f, damaged.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.True(store.TryGet(player, out DamageStateComponent state));
        Assert.Equal(DamageSource.Suffocation, state.DeathCause);
        Assert.Equal(13L, state.DeathTick);
    }

    [Fact]
    public void DamageServiceSkipsCreativeAndAppliesKineticResistance()
    {
        var store = new DataStore();
        int creative = store.Alloc();
        store.AddOrUpdate(creative, new GameModeComponent(GameMode.Creative));
        store.AddOrUpdate(creative, CreateStats(100f, 100f));

        Assert.Equal(0f, DamageService.ApplyDamage(store, creative, 50f, DamageSource.Suffocation).Applied);

        int resistant = store.Alloc();
        store.AddOrUpdate(resistant, new GameModeComponent(GameMode.Adventure));
        StatisticsComponent stats = CreateStats(100f, 100f);
        stats.SetFixed(StatisticIds.KINETIC_RESIST, 0.25f);
        store.AddOrUpdate(resistant, stats);

        DamageResult result = DamageService.ApplyDamage(store, resistant, 40f, DamageSource.Kinetic);
        Assert.Equal(30f, result.Applied, precision: 3);
    }

    [Fact]
    public void JoinSeedingUsesConfiguredVitalMaximums()
    {
        var store = new DataStore();
        int player = store.Alloc();

        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(
            store,
            player,
            new CharacterState(),
            GameMode.Adventure,
            defaultHealthMax: 150f,
            defaultOxygenMax: 200f);

        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        Assert.Equal(150f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        Assert.Equal(150f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(200f, stats.GetFixed(StatisticIds.OXYGEN_MAX), precision: 3);
        Assert.Equal(200f, stats.GetFixed(StatisticIds.OXYGEN), precision: 3);
    }

    [Fact]
    public void ShippedConsumablesParseWithTheirEffects()
    {
        string root = Path.Combine(GetRepoRoot(), "WaywardBeyond.Items", "assets");
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(root));
        IFileParser[] parsers = [new TomlParser<ItemDefinitions>()];
        var parser = new VirtualFileParseService(parsers, vfs);
        var database = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);

        ConsumableDefinition medkit = database.Get(MEDKIT).Value.Consumable!.Value;
        Assert.Equal(40f, medkit.HealHp);
        Assert.Equal(8f, medkit.CooldownSeconds);

        ConsumableDefinition canister = database.Get(O2_CANISTER).Value.Consumable!.Value;
        Assert.Equal(50f, canister.RestoreO2);

        ConsumableDefinition ration = database.Get(RATION).Value.Consumable!.Value;
        Assert.Equal(15f, ration.HealHp);
        Assert.Equal(WELL_FED, ration.GrantBuffID);
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static StatisticsComponent CreateStats(float health, float oxygen)
    {
        var stats = new StatisticsComponent();
        stats.SetFixed(StatisticIds.HEALTH_MAX, 100f);
        stats.SetFixed(StatisticIds.HEALTH, health);
        stats.SetFixed(StatisticIds.OXYGEN_MAX, 100f);
        stats.SetFixed(StatisticIds.OXYGEN, oxygen);
        return stats;
    }

    private static Buff CreateBuff(
        string id,
        float healthPerSecond,
        float oxygenPerSecond,
        BuffStackPolicy stackPolicy = BuffStackPolicy.Refresh
    ) {
        IEnumerable<BuffRegenRate> rates = healthPerSecond != 0f && oxygenPerSecond != 0f
            ? [new BuffRegenRate(StatisticIds.HEALTH, healthPerSecond), new BuffRegenRate(StatisticIds.OXYGEN, oxygenPerSecond)]
            : healthPerSecond != 0f
                ? [new BuffRegenRate(StatisticIds.HEALTH, healthPerSecond)]
                : [new BuffRegenRate(StatisticIds.OXYGEN, oxygenPerSecond)];

        return new Buff
        {
            ID = id,
            Name = "buff.test",
            DurationSeconds = 60f,
            StackPolicy = stackPolicy,
            RegenRates = [.. rates],
        }.Validate().Value;
    }

    private sealed class SurvivalHarness : IDisposable
    {
        private readonly GameplayConfig _config = new();

        public DataStore Store { get; } = new();
        public SimulationStep Step { get; }
        public StubBuffDatabase Buffs { get; } = new();
        public ServerSurvivalSystem Survival { get; }
        public int Player { get; }

        public SurvivalHarness(
            GameMode mode,
            float health = 100f,
            float oxygen = 100f,
            bool pressurized = false
        ) {
            Step = new SimulationStep(Store, new FakePhysics());
            Survival = new ServerSurvivalSystem(Step, _config, Buffs);

            Player = Store.Alloc(Uuid.FromValue(9001));
            Store.AddOrUpdate(Player, new OwnedCharacterComponent());
            Store.AddOrUpdate(Player, new GameModeComponent(mode));
            StatisticsComponent stats = CreateStats(health, oxygen);
            stats.SetFlag(StatisticIds.PRESSURIZED, pressurized);
            Store.AddOrUpdate(Player, stats);
        }

        public void Tick(float delta)
        {
            Step.AlignTo(Step.CurrentSimTick + 1);
            Survival.Tick(delta, Store);
        }

        public StatisticsComponent GetStats()
        {
            return Store.TryGet(Player, out StatisticsComponent stats) ? stats : default;
        }

        public float GetFixed(string id) => GetStats().GetFixed(id);

        public void SetBuffExpiry(string buffId, long expiresTick)
        {
            StatisticsComponent stats = GetStats();
            stats.SetBuffExpiresTick(buffId, expiresTick);
            Store.AddOrUpdate(Player, stats);
        }

        public void SetStatistic(string id, long value)
        {
            StatisticsComponent stats = GetStats();
            stats.SetValue(id, value);
            Store.AddOrUpdate(Player, stats);
        }

        public void Dispose()
        {
            Step.Dispose();
        }
    }

    private sealed class ConsumeHarness
    {
        public DataStore Store { get; } = new();
        public int Player { get; }
        public long SimTick { get; private set; } = 100L;
        public StubBuffDatabase Buffs { get; } = new();
        private readonly StubItemDatabase _items = new();
        private readonly ConsumeItemInteractionHandler _handler;
        private readonly GameMode _mode;

        public ConsumeHarness(GameMode mode, float health = 100f, float oxygen = 100f)
        {
            _handler = new ConsumeItemInteractionHandler(_items, Buffs);
            _mode = mode;

            Player = Store.Alloc(Uuid.FromValue(9002));
            Store.AddOrUpdate(Player, new GameModeComponent(mode));
            Store.AddOrUpdate(Player, new EquipmentComponent(0));
            Store.AddOrUpdate(Player, InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 9)));
            Store.AddOrUpdate(Player, CreateStats(health, oxygen));
        }

        public StatisticsComponent Stats => Store.TryGet(Player, out StatisticsComponent stats) ? stats : default;

        public float GetFixed(string id) => Stats.GetFixed(id);

        public void SetBuffExpiry(string buffId, long expiresTick)
        {
            StatisticsComponent stats = Stats;
            stats.SetBuffExpiresTick(buffId, expiresTick);
            Store.AddOrUpdate(Player, stats);
        }

        public void Give(string id, int count, ConsumableDefinition consumable)
        {
            _items.Add(new Item(id, "item.test", null, 10, null, null, null, null, consumable: consumable));
            if (Store.TryGet(Player, out InventoryComponent inventory))
            {
                InventoryComponent updated = inventory.Clone();
                updated.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData(id, count, 10));
                Store.AddOrUpdate(Player, updated);
            }
        }

        public void Consume(string heldItemId)
        {
            var request = new InteractionRequest(
                Vector3.Zero,
                hint: null,
                InteractionKind.SecondaryPressed,
                placeable: null,
                _mode,
                CharacterDefaults.DEFAULT_REACH);

            var context = new InteractionContext(
                Store,
                Player,
                Uuid.Null,
                sequence: 1,
                world: null!,
                in request,
                heldItemId,
                SimTick,
                1f / 60f);

            _handler.Handle(ref context);
            SimTick++;
        }

        public int GetHotbarCount(string itemId)
        {
            if (!Store.TryGet(Player, out InventoryComponent inventory))
            {
                return 0;
            }

            ItemData item = inventory.Get(new SlotId(SlotGroups.HOTBAR, 0));
            return string.Equals(item.ID, itemId, StringComparison.Ordinal) ? item.Count : 0;
        }
    }

    private sealed class StubItemDatabase : IItemDatabase
    {
        private readonly Dictionary<string, Item> _items = new(StringComparer.Ordinal);

        public void Add(Item item) => _items[item.ID] = item;

        public Result<Item> Get(string id)
        {
            return _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure($"Unknown item \"{id}\".");
        }
    }

    private sealed class StubBuffDatabase : IBuffDatabase
    {
        private readonly List<Buff> _buffs = [];
        private readonly Dictionary<string, Buff> _byId = new(StringComparer.Ordinal);

        public IReadOnlyList<Buff> All => _buffs;

        public void Add(Buff buff)
        {
            _buffs.Add(buff);
            _byId[buff.ID] = buff;
        }

        public Result<Buff> Get(string id)
        {
            return _byId.TryGetValue(id, out Buff buff)
                ? Result<Buff>.FromSuccess(buff)
                : Result<Buff>.FromFailure($"Unknown buff \"{id}\".");
        }
    }

    private sealed class TempItemAssets : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"wb-survival-{Path.GetRandomFileName()}");

        public TempItemAssets(string toml)
        {
            Directory.CreateDirectory(Path.Combine(Root, "items"));
            File.WriteAllText(Path.Combine(Root, "items", "items.toml"), toml);
        }

        public ItemDatabase LoadItems()
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(Root));
            IFileParser[] parsers = [new TomlParser<ItemDefinitions>()];
            var parser = new VirtualFileParseService(parsers, vfs);
            return new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                //  Best-effort temp cleanup.
            }
        }
    }
}
