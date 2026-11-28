#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics;
using Swordfish.Settings;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Systems;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Pins server-authoritative kinetic collision damage, cooldown debouncing, Creative immunity, and mitigation.</summary>
public sealed class CollisionDamageSystemTests
{
    [Fact]
    public void GentleImpactBelowSafeSpeedDealsNoDamage()
    {
        using var harness = new CollisionHarness();

        harness.Impact(harness.Config.CollisionSafeSpeed.Get() - 0.5f);
        harness.Tick();

        Assert.Equal(100f, harness.GetHealth(), precision: 3);
        Assert.False(harness.Store.TryGet(harness.Player, out DamageStateComponent _));
    }

    [Fact]
    public void ImpactAboveSafeSpeedDealsLinearlyScaledDamage()
    {
        using var harness = new CollisionHarness();
        float safeSpeed = harness.Config.CollisionSafeSpeed.Get();
        float perSpeed = harness.Config.CollisionDamagePerSpeed.Get();
        float impact = safeSpeed + 10f;

        harness.Impact(impact);
        harness.Tick();

        Assert.Equal(100f - (10f * perSpeed), harness.GetHealth(), precision: 3);
        Assert.True(harness.Store.TryGet(harness.Player, out DamageStateComponent damage));
        Assert.Equal(DamageSource.KineticImpact, damage.LastSource);
        Assert.Equal(10f * perSpeed, damage.LastAmount, precision: 3);
    }

    [Fact]
    public void DamageIsCappedByConfiguredMaximum()
    {
        using var harness = new CollisionHarness();
        harness.Config.CollisionMaxDamage.Set(30f);

        harness.Impact(100f);
        harness.Tick();

        Assert.Equal(70f, harness.GetHealth(), precision: 3);
    }

    [Fact]
    public void CompoundContactsFromOneImpactApplyDamageOnce()
    {
        using var harness = new CollisionHarness();

        for (var i = 0; i < 6; i++)
        {
            harness.Impact(harness.Config.CollisionSafeSpeed.Get() + 10f);
        }

        harness.Tick();

        float expected = 100f - (10f * harness.Config.CollisionDamagePerSpeed.Get());
        Assert.Equal(expected, harness.GetHealth(), precision: 3);
    }

    [Fact]
    public void CooldownWindowSuppressesFurtherImpactsUntilItElapses()
    {
        using var harness = new CollisionHarness();
        float impact = harness.Config.CollisionSafeSpeed.Get() + 10f;
        float damage = 10f * harness.Config.CollisionDamagePerSpeed.Get();

        harness.Impact(impact);
        harness.Tick();

        harness.Tick(0.1f);
        harness.Impact(impact);
        harness.Tick();
        Assert.Equal(100f - damage, harness.GetHealth(), precision: 3);

        harness.Tick(harness.Config.CollisionCooldownSeconds.Get());
        harness.Impact(impact);
        harness.Tick();
        Assert.Equal(100f - (damage * 2f), harness.GetHealth(), precision: 3);
    }

    [Fact]
    public void CreativeModePlayersTakeNoCollisionDamage()
    {
        using var harness = new CollisionHarness(GameMode.Creative);

        harness.Impact(50f);
        harness.Tick();

        Assert.Equal(100f, harness.GetHealth(), precision: 3);
        Assert.False(harness.Store.TryGet(harness.Player, out DamageStateComponent _));
    }

    [Fact]
    public void KineticResistanceMitigatesCollisionDamage()
    {
        using var harness = new CollisionHarness();
        StatisticsComponent stats = harness.GetStats();
        stats.SetFixed(StatisticIds.KINETIC_RESIST, 0.5f);
        harness.Store.AddOrUpdate(harness.Player, stats);

        harness.Impact(harness.Config.CollisionSafeSpeed.Get() + 10f);
        harness.Tick();

        Assert.Equal(75f, harness.GetHealth(), precision: 3);
    }

    [Fact]
    public void ContactsWithoutAnOwnedCharacterTakeNoDamage()
    {
        using var harness = new CollisionHarness();

        harness.Physics.Raise(CreateContact(harness.Rock, harness.OtherRock, 50f, PhysicsContactKind.Added));
        harness.Tick();

        Assert.Equal(100f, harness.GetHealth(), precision: 3);
        Assert.False(harness.Store.TryGet(harness.Player, out DamageStateComponent _));
    }

    [Fact]
    public void PersistedContactsAreIgnored()
    {
        using var harness = new CollisionHarness();

        harness.Physics.Raise(CreateContact(harness.Player, harness.Rock, 50f, PhysicsContactKind.Persisted));
        harness.Tick();

        Assert.Equal(100f, harness.GetHealth(), precision: 3);
    }

    [Fact]
    public void PlayerOnEitherSideOfTheContactTakesDamage()
    {
        using var harness = new CollisionHarness();

        harness.Physics.Raise(CreateContact(harness.Rock, harness.Player, 50f, PhysicsContactKind.Added));
        harness.Tick();

        Assert.True(harness.GetHealth() < 100f);
    }

    [Fact]
    public void ServerPhysicsForwardsJoltContactsAndDrivesTheSystem()
    {
        var store = new DataStore();
        int floor = AddPhysicsBox(store, new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), BodyType.Static, Vector3.Zero);
        int player = AddPhysicsBox(store, new Vector3(0f, 0.75f, 0f), Vector3.One, BodyType.Dynamic, new Vector3(0f, -20f, 0f));

        store.AddOrUpdate(player, new OwnedCharacterComponent());
        store.AddOrUpdate(player, new GameModeComponent(GameMode.Adventure));
        var stats = new StatisticsComponent();
        stats.SetFixed(StatisticIds.HEALTH_MAX, 100f);
        stats.SetFixed(StatisticIds.HEALTH, 100f);
        store.AddOrUpdate(player, stats);

        var config = new GameplayConfig();
        var physics = new ServerPhysicsSystem(NullLoggerFactory.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);

        var events = new List<PhysicsContactEvent>();
        physics.ContactResolved += events.Add;

        using var step = new SimulationStep(store, physics);
        var damage = new ServerCollisionDamageSystem(step, config, physics);

        //  Tick the physics slot and the consumer the way the world order does. The shared step
        //  advances itself from the physics slot's fixed update.
        float delta = physics.FixedTimestamp * 1.05f;
        for (var i = 0; i < 30; i++)
        {
            physics.Tick(delta, store);
            damage.Tick(delta, store);
        }

        Assert.NotEmpty(events);
        Assert.Contains(events, contact =>
            (contact.EntityA == floor && contact.EntityB == player)
            || (contact.EntityA == player && contact.EntityB == floor));
        Assert.True(store.TryGet(player, out StatisticsComponent damaged));
        Assert.True(damaged.GetFixed(StatisticIds.HEALTH) < 100f);
    }

    private static int AddPhysicsBox(
        DataStore store,
        Vector3 position,
        Vector3 extents,
        BodyType bodyType,
        Vector3 velocity
    ) {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent(
            bodyType == BodyType.Static ? Layers.NON_MOVING : Layers.MOVING,
            bodyType,
            CollisionDetection.Continuous,
            velocity,
            Vector3.Zero));
        store.AddOrUpdate(entity, new ColliderComponent(new Swordfish.Library.Types.Shapes.Box3(extents)));
        return entity;
    }

    private static PhysicsContactEvent CreateContact(int entityA, int entityB, float impactSpeed, PhysicsContactKind kind)
    {
        return new PhysicsContactEvent(
            entityA,
            entityB,
            Vector3.UnitX,
            Vector3.Zero,
            new Vector3(impactSpeed, 0f, 0f),
            impactSpeed,
            kind
        );
    }

    private sealed class CollisionHarness : IDisposable
    {
        public GameplayConfig Config { get; } = new();
        public DataStore Store { get; } = new();
        public FakeServerPhysics Physics { get; } = new();
        public SimulationStep Step { get; }
        public ServerCollisionDamageSystem Damage { get; }
        public int Player { get; }
        public int Rock { get; }
        public int OtherRock { get; }

        public CollisionHarness(GameMode mode = GameMode.Adventure)
        {
            Step = new SimulationStep(Store, Physics);
            Damage = new ServerCollisionDamageSystem(Step, Config, Physics);

            Player = Store.Alloc(Uuid.FromValue(9101));
            Store.AddOrUpdate(Player, new OwnedCharacterComponent());
            Store.AddOrUpdate(Player, new GameModeComponent(mode));

            var stats = new StatisticsComponent();
            stats.SetFixed(StatisticIds.HEALTH_MAX, 100f);
            stats.SetFixed(StatisticIds.HEALTH, 100f);
            stats.SetFixed(StatisticIds.OXYGEN_MAX, 100f);
            stats.SetFixed(StatisticIds.OXYGEN, 100f);
            Store.AddOrUpdate(Player, stats);

            Rock = Store.Alloc(Uuid.FromValue(9102));
            OtherRock = Store.Alloc(Uuid.FromValue(9103));
        }

        public void Impact(float impactSpeed)
        {
            Physics.Raise(CreateContact(Player, Rock, impactSpeed, PhysicsContactKind.Added));
        }

        public void Tick(float delta = 1f / 60f)
        {
            Step.AlignTo(Step.CurrentSimTick + 1);
            Damage.Tick(delta, Store);
        }

        public StatisticsComponent GetStats()
        {
            return Store.TryGet(Player, out StatisticsComponent stats) ? stats : default;
        }

        public float GetHealth() => GetStats().GetFixed(StatisticIds.HEALTH);

        public void Dispose()
        {
            Step.Dispose();
        }
    }

    private sealed class FakeServerPhysics : IServerWorldPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public event Action<PhysicsContactEvent>? ContactResolved;

        public RaycastResult Raycast(in Ray ray) => default;

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }

        public void Tick(float delta, DataStore store) { }

        public void Raise(in PhysicsContactEvent contact)
        {
            ContactResolved?.Invoke(contact);
        }
    }
}
