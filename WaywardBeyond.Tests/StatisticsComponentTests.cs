#nullable enable
using System;
using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Statistics;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers fixed-point math, buff duration calculations, span parsing, and change-gated writes on <see cref="StatisticsComponent"/>.</summary>
public sealed class StatisticsComponentTests
{
    [Fact]
    public void FixedPointConversionsRoundTripAndRejectNonFiniteInputs()
    {
        Assert.True(StatisticIds.TryToFixed(100f, out long hundred));
        Assert.Equal(100_000L, hundred);
        Assert.Equal(100f, StatisticIds.FromFixed(hundred));

        Assert.True(StatisticIds.TryToFixed(42.375f, out long fractional));
        Assert.Equal(42_375L, fractional);
        Assert.Equal(42.375f, StatisticIds.FromFixed(fractional));

        Assert.False(StatisticIds.TryToFixed(float.NaN, out _));
        Assert.False(StatisticIds.TryToFixed(float.PositiveInfinity, out _));
        Assert.False(StatisticIds.TryToFixed(float.NegativeInfinity, out _));
        Assert.Equal(0L, StatisticIds.ToFixed(float.NaN));

        var stats = new StatisticsComponent();
        Assert.True(stats.SetFixed(StatisticIds.HEALTH, 87.5f));
        Assert.False(stats.SetFixed(StatisticIds.HEALTH, 87.5f));
        Assert.False(stats.SetFixed(StatisticIds.HEALTH, float.NaN));
        Assert.True(stats.TryGetFixed(StatisticIds.HEALTH, out float readBack));
        Assert.Equal(87.5f, readBack);
    }

    [Fact]
    public void BuffExpiryAndRemainingDurationCalculationsAreAccurate()
    {
        const float fixedDt = 1f / 64f;
        long expiresTick = StatisticIds.ComputeExpiresTick(currentSimTick: 1000L, durationSeconds: 30f, fixedDt);

        Assert.Equal(1000L + (30L * 64L), expiresTick);
        Assert.Equal(30f, StatisticIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L, fixedDt), precision: 3);
        Assert.Equal(15f, StatisticIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L + (15L * 64L), fixedDt), precision: 3);
        Assert.Equal(0f, StatisticIds.ComputeRemainingSeconds(expiresTick, currentSimTick: expiresTick, fixedDt));
        Assert.Equal(0f, StatisticIds.ComputeRemainingSeconds(expiresTick, currentSimTick: expiresTick + 10L, fixedDt));
        Assert.Equal(0f, StatisticIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L, fixedDt: 0f));
        Assert.Equal(0f, StatisticIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L, fixedDt: float.NaN));
        Assert.True(StatisticIds.ComputeRemainingSeconds(long.MaxValue, currentSimTick: -10L, fixedDt) > 0f);

        var stats = new StatisticsComponent();
        Assert.True(stats.SetBuffExpiresTick("wb:well_fed", expiresTick));
        Assert.True(stats.TryGetBuffExpiresTick("wb:well_fed", out long stored));
        Assert.Equal(expiresTick, stored);
        Assert.Equal(30f, stats.GetBuffRemainingSeconds("wb:well_fed", 1000L, fixedDt), precision: 3);

        var extremeBuff = new StatisticsComponent();
        extremeBuff.SetBuffExpiresTick("wb:well_fed", long.MaxValue);
        Statistic[]? persistedExtreme = StatisticsPersistence.CaptureServerStatistics(extremeBuff.Entries, currentSimTick: -10L);
        Assert.NotNull(persistedExtreme);
        Statistic extremeEntry = Assert.Single(persistedExtreme!);
        Assert.Equal(StatisticIds.BuffRemainingTicks("wb:well_fed"), extremeEntry.ID);
        Assert.Equal(long.MaxValue, extremeEntry.Value);
        Assert.Null(StatisticsPersistence.MergeClientStatistics([], ReadOnlySpan<Statistic>.Empty));
    }

    [Fact]
    public void BuffKeySpanHelpersMatchAndParseWithoutInvalidMatches()
    {
        string key = StatisticIds.BuffExpiresTick("wb:well_fed");
        Assert.Equal("wb:buff.wb:well_fed.expires_tick", key);

        Assert.True(StatisticIds.IsBuffExpiresTickKey(key));
        Assert.True(StatisticIds.MatchesBuffExpiresTick(key, "wb:well_fed"));
        Assert.False(StatisticIds.MatchesBuffExpiresTick(key, "wb:other"));

        Assert.True(StatisticIds.TryParseBuffId(key, out var parsedId));
        Assert.Equal("wb:well_fed", parsedId.ToString());

        Assert.False(StatisticIds.TryParseBuffId("wb:buff..expires_tick", out _));
        Assert.False(StatisticIds.TryParseBuffId("wb:buff.   .expires_tick", out _));
        Assert.False(StatisticIds.TryParseBuffId("wb:health", out _));

        string remainingKey = StatisticIds.BuffRemainingTicks("wb:well_fed");
        Assert.Equal("wb:buff.wb:well_fed.remaining_ticks", remainingKey);
        Assert.True(StatisticIds.IsBuffRemainingTicksKey(remainingKey));
        Assert.True(StatisticIds.TryParseBuffRemainingTicksId(remainingKey, out var parsedRemainingId));
        Assert.Equal("wb:well_fed", parsedRemainingId.ToString());
        Assert.False(StatisticIds.IsBuffRemainingTicksKey(key));
        Assert.False(StatisticIds.TryParseBuffRemainingTicksId("wb:buff..remaining_ticks", out _));
    }

    [Fact]
    public void ApplyBuffHandlesRefreshExtendAndIgnorePolicies()
    {
        const float fixedDt = 1f / 60f;
        var stats = new StatisticsComponent();

        Buff refreshBuff = new Buff
        {
            ID = "wb:refresh",
            Name = "buff.refresh",
            DurationSeconds = 10f,
            StackPolicy = BuffStackPolicy.Refresh,
        }.Validate().Value;

        Assert.True(stats.ApplyBuff(in refreshBuff, currentSimTick: 100L, fixedDt));
        Assert.Equal(700L, stats.GetValue(refreshBuff.ExpiryStatId));
        Assert.True(stats.ApplyBuff(in refreshBuff, currentSimTick: 250L, fixedDt));
        Assert.Equal(850L, stats.GetValue(refreshBuff.ExpiryStatId));

        Buff extendBuff = new Buff
        {
            ID = "wb:extend",
            Name = "buff.extend",
            DurationSeconds = 10f,
            StackPolicy = BuffStackPolicy.Extend,
        }.Validate().Value;

        Assert.True(stats.ApplyBuff(in extendBuff, currentSimTick: 100L, fixedDt));
        Assert.Equal(700L, stats.GetValue(extendBuff.ExpiryStatId));
        Assert.True(stats.ApplyBuff(in extendBuff, currentSimTick: 200L, fixedDt));
        Assert.Equal(1300L, stats.GetValue(extendBuff.ExpiryStatId));

        Buff ignoreBuff = new Buff
        {
            ID = "wb:ignore",
            Name = "buff.ignore",
            DurationSeconds = 10f,
            StackPolicy = BuffStackPolicy.Ignore,
        }.Validate().Value;

        Assert.True(stats.ApplyBuff(in ignoreBuff, currentSimTick: 100L, fixedDt));
        Assert.Equal(700L, stats.GetValue(ignoreBuff.ExpiryStatId));
        Assert.False(stats.ApplyBuff(in ignoreBuff, currentSimTick: 200L, fixedDt));
        Assert.Equal(700L, stats.GetValue(ignoreBuff.ExpiryStatId));
        Assert.True(stats.ApplyBuff(in ignoreBuff, currentSimTick: 700L, fixedDt));
        Assert.Equal(1300L, stats.GetValue(ignoreBuff.ExpiryStatId));

        Buff invalidPolicyBuff = refreshBuff with { StackPolicy = (BuffStackPolicy)99 };
        Assert.False(stats.ApplyBuff(in invalidPolicyBuff, currentSimTick: 100L, fixedDt));
        Assert.False(stats.ApplyBuff(in refreshBuff, currentSimTick: -1L, fixedDt));

        //  Direct unvalidated construction (ExpiryStatId unset) falls back to ID cleanly.
        var unvalidatedBuff = new Buff
        {
            ID = "wb:direct_buff",
            Name = "buff.direct",
            DurationSeconds = 5f,
            StackPolicy = BuffStackPolicy.Refresh,
        };
        Assert.True(stats.ApplyBuff(in unvalidatedBuff, currentSimTick: 100L, fixedDt));
        Assert.True(stats.TryGetBuffExpiresTick("wb:direct_buff", out long directExpiry));
        Assert.Equal(400L, directExpiry);

        var directStore = new DataStore();
        int entity = directStore.Alloc();
        Assert.True(directStore.ApplyBuff(entity, in unvalidatedBuff, currentSimTick: 200L, fixedDt));
        Assert.True(directStore.TryGet(entity, out StatisticsComponent storedDirect));
        Assert.True(storedDirect.TryGetBuffExpiresTick("wb:direct_buff", out long storedDirectExpiry));
        Assert.Equal(500L, storedDirectExpiry);
    }

    [Fact]
    public void EntityStatisticModifierSourceHandlesIntegerFixedPointAndBuffTimerKeys()
    {
        var store = new DataStore();
        int entity = store.Alloc();

        var stats = new StatisticsComponent();
        stats.SetValue(StatisticIds.RESOLVE, 5L);
        stats.SetFlag(StatisticIds.PRESSURIZED, true);
        stats.SetFixed(StatisticIds.HEALTH_MAX, 120f);
        stats.SetFixed("mod:jump_boost", 2.5f);
        stats.SetBuffExpiresTick("wb:well_fed", 5000L);
        store.AddOrUpdate(entity, stats);

        var resolver = new StatResolver([new EntityStatisticModifierSource()]);

        float resolve = resolver.Evaluate(new StatQuery(StatisticIds.RESOLVE, defaultBase: 0f, subject: StatSubject.Entity, store: store, entity: entity));
        float pressurized = resolver.Evaluate(new StatQuery(StatisticIds.PRESSURIZED, defaultBase: 0f, subject: StatSubject.Entity, store: store, entity: entity));
        float healthMax = resolver.Evaluate(new StatQuery(StatisticIds.HEALTH_MAX, defaultBase: 0f, subject: StatSubject.Entity, store: store, entity: entity));
        float jumpBoost = resolver.Evaluate(new StatQuery("mod:jump_boost", defaultBase: 1f, subject: StatSubject.Entity, store: store, entity: entity));
        float buffExpiry = resolver.Evaluate(new StatQuery(StatisticIds.BuffExpiresTick("wb:well_fed"), defaultBase: 0f, subject: StatSubject.Entity, store: store, entity: entity));

        Assert.Equal(5f, resolve, precision: 5);
        Assert.Equal(1f, pressurized, precision: 5);
        Assert.Equal(120f, healthMax, precision: 5);
        Assert.Equal(3.5f, jumpBoost, precision: 5);
        Assert.Equal(0f, buffExpiry, precision: 5);

        Assert.True(StatisticIds.IsNamespacedStatId("wb:health"));
        Assert.True(StatisticIds.IsNamespacedStatId("wb:strength"));
        Assert.True(StatisticIds.IsNamespacedStatId("mod:shield"));
        Assert.True(StatisticIds.IsNamespacedStatId("my_mod:mana.max"));
        Assert.False(StatisticIds.IsNamespacedStatId("bricks.broken:wb:rock"));
        Assert.False(StatisticIds.IsNamespacedStatId("bricks.placed:wb:rock"));
        Assert.False(StatisticIds.IsNamespacedStatId("bricks.broken"));
        Assert.False(StatisticIds.IsNamespacedStatId(" :foo"));
        Assert.False(StatisticIds.IsNamespacedStatId("wb:"));
        Assert.False(StatisticIds.IsNamespacedStatId("wb:   "));
        Assert.False(StatisticIds.IsNamespacedStatId(""));
    }

    [Fact]
    public void PruneExpiredBuffsRemovesOnlyExpiredEntries()
    {
        var stats = new StatisticsComponent();
        stats.SetValue(StatisticIds.HEALTH, 90_000L);
        stats.SetBuffExpiresTick("wb:expired_exact", 100L);
        stats.SetBuffExpiresTick("wb:expired_old", 50L);
        stats.SetBuffExpiresTick("wb:active", 250L);

        Assert.True(stats.PruneExpiredBuffs(currentSimTick: 100L));
        Assert.Equal(2, stats.Count);
        Assert.Equal(90_000L, stats.GetValue(StatisticIds.HEALTH));
        Assert.False(stats.TryGetBuffExpiresTick("wb:expired_exact", out _));
        Assert.False(stats.TryGetBuffExpiresTick("wb:expired_old", out _));
        Assert.True(stats.TryGetBuffExpiresTick("wb:active", out long activeExpiry));
        Assert.Equal(250L, activeExpiry);

        Statistic[]? snapshot = stats.Entries;
        Assert.False(stats.PruneExpiredBuffs(currentSimTick: 100L));
        Assert.Same(snapshot, stats.Entries);
    }

    [Fact]
    public void ChangeGatedStoreWritesDoNotMarkDirtyWhenValuesAreUnchanged()
    {
        var store = new DataStore();
        int entity = store.Alloc();

        Assert.True(store.SetStatisticFixed(entity, StatisticIds.HEALTH, 100f));
        Assert.True(store.SetStatisticFlag(entity, StatisticIds.PRESSURIZED, true));
        Assert.True(store.SetBuffExpiresTick(entity, "wb:well_fed", 500L));
        store.ClearDirty<StatisticsComponent>(entity);

        //  Idle identical writes must never re-dirty the component.
        Assert.False(store.SetStatisticFixed(entity, StatisticIds.HEALTH, 100f));
        Assert.False(store.SetStatisticFlag(entity, StatisticIds.PRESSURIZED, true));
        Assert.False(store.SetBuffExpiresTick(entity, "wb:well_fed", 500L));
        Assert.False(store.RemoveStatistic(entity, StatisticIds.OXYGEN));
        Assert.False(store.RemoveBuff(entity, "wb:missing"));
        Assert.False(store.PruneExpiredBuffs(entity, currentSimTick: 499L));
        Assert.False(store.IsDirty<StatisticsComponent>(entity));

        //  Mutating a value marks dirty immediately.
        Assert.True(store.SetStatisticFixed(entity, StatisticIds.HEALTH, 99.5f));
        Assert.True(store.IsDirty<StatisticsComponent>(entity));

        store.ClearDirty<StatisticsComponent>(entity);
        Assert.True(store.PruneExpiredBuffs(entity, currentSimTick: 500L));
        Assert.True(store.IsDirty<StatisticsComponent>(entity));
    }
}
