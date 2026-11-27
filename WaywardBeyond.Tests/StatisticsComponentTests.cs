#nullable enable
using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Components;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers fixed-point math, buff duration calculations, span parsing, and change-gated writes on <see cref="StatisticsComponent"/>.</summary>
public sealed class StatisticsComponentTests
{
    [Fact]
    public void FixedPointConversionsRoundTripAndRejectNonFiniteInputs()
    {
        Assert.True(StatIds.TryToFixed(100f, out long hundred));
        Assert.Equal(100_000L, hundred);
        Assert.Equal(100f, StatIds.FromFixed(hundred));

        Assert.True(StatIds.TryToFixed(42.375f, out long fractional));
        Assert.Equal(42_375L, fractional);
        Assert.Equal(42.375f, StatIds.FromFixed(fractional));

        Assert.False(StatIds.TryToFixed(float.NaN, out _));
        Assert.False(StatIds.TryToFixed(float.PositiveInfinity, out _));
        Assert.False(StatIds.TryToFixed(float.NegativeInfinity, out _));
        Assert.Equal(0L, StatIds.ToFixed(float.NaN));

        var stats = new StatisticsComponent();
        Assert.True(stats.SetFixed(StatIds.HEALTH, 87.5f));
        Assert.False(stats.SetFixed(StatIds.HEALTH, 87.5f));
        Assert.False(stats.SetFixed(StatIds.HEALTH, float.NaN));
        Assert.True(stats.TryGetFixed(StatIds.HEALTH, out float readBack));
        Assert.Equal(87.5f, readBack);
    }

    [Fact]
    public void BuffExpiryAndRemainingDurationCalculationsAreAccurate()
    {
        const float fixedDt = 1f / 64f;
        long expiresTick = StatIds.ComputeExpiresTick(currentSimTick: 1000L, durationSeconds: 30f, fixedDt);

        Assert.Equal(1000L + (30L * 64L), expiresTick);
        Assert.Equal(30f, StatIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L, fixedDt), precision: 3);
        Assert.Equal(15f, StatIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L + (15L * 64L), fixedDt), precision: 3);
        Assert.Equal(0f, StatIds.ComputeRemainingSeconds(expiresTick, currentSimTick: expiresTick, fixedDt));
        Assert.Equal(0f, StatIds.ComputeRemainingSeconds(expiresTick, currentSimTick: expiresTick + 10L, fixedDt));
        Assert.Equal(0f, StatIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L, fixedDt: 0f));
        Assert.Equal(0f, StatIds.ComputeRemainingSeconds(expiresTick, currentSimTick: 1000L, fixedDt: float.NaN));
        Assert.True(StatIds.ComputeRemainingSeconds(long.MaxValue, currentSimTick: -10L, fixedDt) > 0f);

        var stats = new StatisticsComponent();
        Assert.True(stats.SetBuffExpiresTick("wb:well_fed", expiresTick));
        Assert.True(stats.TryGetBuffExpiresTick("wb:well_fed", out long stored));
        Assert.Equal(expiresTick, stored);
        Assert.Equal(30f, stats.GetBuffRemainingSeconds("wb:well_fed", 1000L, fixedDt), precision: 3);

        var extremeBuff = new StatisticsComponent();
        extremeBuff.SetBuffExpiresTick("wb:well_fed", long.MaxValue);
        Statistic[]? persistedExtreme = StatisticsComponentExtensions.CapturePersistedServerStatistics(in extremeBuff, currentSimTick: -10L);
        Assert.NotNull(persistedExtreme);
        Statistic extremeEntry = Assert.Single(persistedExtreme!);
        Assert.Equal(StatIds.BuffRemainingTicks("wb:well_fed"), extremeEntry.ID);
        Assert.Equal(long.MaxValue, extremeEntry.Value);
    }

    [Fact]
    public void BuffKeySpanHelpersMatchAndParseWithoutInvalidMatches()
    {
        string key = StatIds.BuffExpiresTick("wb:well_fed");
        Assert.Equal("wb:buff.wb:well_fed.expires_tick", key);

        Assert.True(StatIds.IsBuffExpiresTickKey(key));
        Assert.True(StatIds.MatchesBuffExpiresTick(key, "wb:well_fed"));
        Assert.False(StatIds.MatchesBuffExpiresTick(key, "wb:other"));

        Assert.True(StatIds.TryParseBuffId(key, out var parsedId));
        Assert.Equal("wb:well_fed", parsedId.ToString());

        Assert.False(StatIds.TryParseBuffId("wb:buff..expires_tick", out _));
        Assert.False(StatIds.TryParseBuffId("wb:buff.   .expires_tick", out _));
        Assert.False(StatIds.TryParseBuffId("wb:health", out _));

        string remainingKey = StatIds.BuffRemainingTicks("wb:well_fed");
        Assert.Equal("wb:buff.wb:well_fed.remaining_ticks", remainingKey);
        Assert.True(StatIds.IsBuffRemainingTicksKey(remainingKey));
        Assert.True(StatIds.TryParseBuffRemainingTicksId(remainingKey, out var parsedRemainingId));
        Assert.Equal("wb:well_fed", parsedRemainingId.ToString());
        Assert.False(StatIds.IsBuffRemainingTicksKey(key));
        Assert.False(StatIds.TryParseBuffRemainingTicksId("wb:buff..remaining_ticks", out _));
    }

    [Fact]
    public void ApplyBuffHandlesRefreshExtendAndIgnorePolicies()
    {
        const float fixedDt = 1f / 60f;
        var stats = new StatisticsComponent();

        BuffDefinition refreshBuff = new BuffDefinition
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

        BuffDefinition extendBuff = new BuffDefinition
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

        BuffDefinition ignoreBuff = new BuffDefinition
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

        BuffDefinition invalidPolicyBuff = refreshBuff with { StackPolicy = (BuffStackPolicy)99 };
        Assert.False(stats.ApplyBuff(in invalidPolicyBuff, currentSimTick: 100L, fixedDt));
        Assert.False(stats.ApplyBuff(in refreshBuff, currentSimTick: -1L, fixedDt));
    }

    [Fact]
    public void PruneExpiredBuffsRemovesOnlyExpiredEntries()
    {
        var stats = new StatisticsComponent();
        stats.SetValue(StatIds.HEALTH, 90_000L);
        stats.SetBuffExpiresTick("wb:expired_exact", 100L);
        stats.SetBuffExpiresTick("wb:expired_old", 50L);
        stats.SetBuffExpiresTick("wb:active", 250L);

        Assert.True(stats.PruneExpiredBuffs(currentSimTick: 100L));
        Assert.Equal(2, stats.Count);
        Assert.Equal(90_000L, stats.GetValue(StatIds.HEALTH));
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

        Assert.True(store.SetStatisticFixed(entity, StatIds.HEALTH, 100f));
        Assert.True(store.SetStatisticFlag(entity, StatIds.PRESSURIZED, true));
        Assert.True(store.SetBuffExpiresTick(entity, "wb:well_fed", 500L));
        store.ClearDirty<StatisticsComponent>(entity);

        //  Idle identical writes must never re-dirty the component.
        Assert.False(store.SetStatisticFixed(entity, StatIds.HEALTH, 100f));
        Assert.False(store.SetStatisticFlag(entity, StatIds.PRESSURIZED, true));
        Assert.False(store.SetBuffExpiresTick(entity, "wb:well_fed", 500L));
        Assert.False(store.RemoveStatistic(entity, StatIds.OXYGEN));
        Assert.False(store.RemoveBuff(entity, "wb:missing"));
        Assert.False(store.PruneExpiredBuffs(entity, currentSimTick: 499L));
        Assert.False(store.IsDirty<StatisticsComponent>(entity));

        //  Mutating a value marks dirty immediately.
        Assert.True(store.SetStatisticFixed(entity, StatIds.HEALTH, 99.5f));
        Assert.True(store.IsDirty<StatisticsComponent>(entity));

        store.ClearDirty<StatisticsComponent>(entity);
        Assert.True(store.PruneExpiredBuffs(entity, currentSimTick: 500L));
        Assert.True(store.IsDirty<StatisticsComponent>(entity));
    }
}
