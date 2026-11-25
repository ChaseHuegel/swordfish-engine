using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StbImageNext;
using StbImageNext.Encoders;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

public class ParticleSystemTests
{
    private sealed class DictionaryAssetDatabase<T> : IAssetDatabase<T>
    {
        public readonly Dictionary<string, T> Assets = new(StringComparer.Ordinal);

        public Result<T> Get(string id)
        {
            return Assets.TryGetValue(id, out T? value)
                ? Result<T>.FromSuccess(value)
                : Result<T>.FromFailure($"Unknown asset \"{id}\"");
        }
    }

    private static ParticleEffect CreateEffect(
        string id = "test_effect",
        Texture? texture = null,
        ParticleBlendMode blendMode = ParticleBlendMode.Additive,
        float rate = 10f,
        int burstCount = 5,
        int maxParticles = 64,
        float lifetimeMin = 1f,
        float lifetimeMax = 1f,
        float speedMin = 2f,
        float speedMax = 2f,
        float spreadDegrees = 0f,
        float spawnRadius = 0f,
        float startSizeMin = 0.2f,
        float startSizeMax = 0.2f,
        float endSizeMin = 0.05f,
        float endSizeMax = 0.05f,
        Vector4? startColor = null,
        Vector4? secondaryStartColor = null,
        Vector4? endColor = null,
        float emissiveIntensity = 1.5f,
        Vector3? gravity = null,
        float drag = 0f
    ) {
        return new ParticleEffect(
            id,
            texture,
            blendMode,
            rate,
            burstCount,
            maxParticles,
            new ParticleRange(lifetimeMin, lifetimeMax),
            new ParticleRange(speedMin, speedMax),
            spreadDegrees,
            spawnRadius,
            new ParticleRange(startSizeMin, startSizeMax),
            new ParticleRange(endSizeMin, endSizeMax),
            startColor ?? new Vector4(1f, 0.5f, 0.2f, 1f),
            secondaryStartColor,
            endColor ?? new Vector4(1f, 0f, 0f, 0f),
            emissiveIntensity,
            gravity ?? Vector3.Zero,
            drag,
            ParticleRange.Constant(0f),
            ParticleRange.Constant(0f)
        );
    }

    [Fact]
    public void ParticleEffectDefinition_ParsesDefaultsAndFullTomlAndDecodesLinearColors()
    {
        ParticleEffectDefinition defaults = Toml.To<ParticleEffectDefinition>("");
        Result<ParticleEffect> defaultResult = defaults.Build("defaults", null);
        Assert.True(defaultResult.Success);
        Assert.Equal(ParticleBlendMode.Alpha, defaultResult.Value.BlendMode);
        Assert.Equal(1f, defaultResult.Value.StartColor.X, precision: 3);
        Assert.Equal(1f, defaultResult.Value.StartColor.W, precision: 3);

        const string toml = """
            BlendMode = "Additive"
            Rate = 45.0
            BurstCount = 12
            MaxParticles = 96
            LifetimeMin = 0.25
            LifetimeMax = 0.5
            SpeedMin = 1.5
            SpeedMax = 3.0
            SpreadDegrees = 35.0
            SpawnRadius = 0.1
            StartSizeMin = 0.08
            StartSizeMax = 0.12
            EndSizeMin = 0.01
            EndSizeMax = 0.02
            StartColor = "#feae34"
            SecondaryStartColor = "#fee76180"
            EndColor = "#e43b44"
            StartAlpha = 0.9
            EndAlpha = 0.0
            EmissiveIntensity = 2.5
            Drag = 1.2
            Gravity = { X = 0.0, Y = -2.0, Z = 0.0 }
            InitialRotationMin = 0.0
            InitialRotationMax = 90.0
            AngularVelocityMin = -45.0
            AngularVelocityMax = 45.0
            """;

        ParticleEffectDefinition def = Toml.To<ParticleEffectDefinition>(toml);
        Result<ParticleEffect> result = def.Build("plasma", null);
        Assert.True(result.Success);

        ParticleEffect effect = result.Value;
        Assert.Equal(ParticleBlendMode.Additive, effect.BlendMode);
        Assert.Equal(45f, effect.Rate);
        Assert.Equal(12, effect.BurstCount);
        Assert.Equal(96, effect.MaxParticles);
        Assert.Equal(0.25f, effect.Lifetime.Min);
        Assert.Equal(0.5f, effect.Lifetime.Max);
        Assert.Equal(2.5f, effect.EmissiveIntensity);
        Assert.Equal(new Vector3(0f, -2f, 0f), effect.Gravity);

        //  #feae34 -> sRGB (254/255, 174/255, 52/255) decoded to linear
        Assert.Equal(Tonemapper.SrgbToLinear(254f / 255f), effect.StartColor.X, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(174f / 255f), effect.StartColor.Y, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(52f / 255f), effect.StartColor.Z, precision: 4);
        Assert.Equal(0.9f, effect.StartColor.W, precision: 4);

        Assert.True(effect.SecondaryStartColor.HasValue);
        Assert.Equal(128f / 255f, effect.SecondaryStartColor.Value.W, precision: 4);
    }

    [Theory]
    [InlineData("LifetimeMin = 0.0\nLifetimeMax = 0.5")]
    [InlineData("LifetimeMin = 0.6\nLifetimeMax = 0.4")]
    [InlineData("SpeedMin = -1.0\nSpeedMax = 1.0")]
    [InlineData("SpreadDegrees = 181.0")]
    [InlineData("StartColor = \"not_hex\"")]
    [InlineData("SecondaryStartColor = \"#12\"")]
    [InlineData("StartAlpha = 1.5")]
    [InlineData("EmissiveIntensity = -0.5")]
    [InlineData("MaxParticles = 0")]
    public void ParticleEffectDefinition_RejectsInvalidValues(string toml)
    {
        ParticleEffectDefinition def = Toml.To<ParticleEffectDefinition>(toml);
        Result<ParticleEffect> result = def.Build("bad", null);
        Assert.False(result.Success);
    }

    [Fact]
    public void ParticleEffectDatabase_LoadsTomlFilesAndResolvesTexturesFromVfs()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "sf_particles_" + Path.GetRandomFileName());
        string particlesDir = Path.Combine(tempRoot, "particles", "sub");
        string texturesDir = Path.Combine(tempRoot, "textures");
        Directory.CreateDirectory(particlesDir);
        Directory.CreateDirectory(texturesDir);

        try
        {
            byte[] png = PngEncoder.EncodeToByteArray([255, 255, 255, 255], 1, 1, ColorComponents.RedGreenBlueAlpha);
            File.WriteAllBytes(Path.Combine(texturesDir, "spark.png"), png);
            File.WriteAllText(Path.Combine(particlesDir, "sparks.toml"), """
                Texture = "spark.png"
                BlendMode = "Additive"
                BurstCount = 8
                StartColor = "#ffffff"
                EndColor = "#feae34"
                """);
            File.WriteAllText(Path.Combine(particlesDir, "missing_tex.toml"), """
                Texture = "does_not_exist.png"
                """);

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(tempRoot));
            var parseService = new VirtualFileParseService(
                [new TextureParser(), new TomlParser<ParticleEffectDefinition>()],
                vfs
            );
            var textureDb = new TextureDatabase(NullLogger<TextureDatabase>.Instance, parseService, vfs);
            var effectDb = new ParticleEffectDatabase(NullLogger<ParticleEffectDatabase>.Instance, parseService, vfs, textureDb);

            Result<ParticleEffect> loaded = effectDb.Get("sub/sparks");
            Assert.True(loaded.Success);
            Assert.NotNull(loaded.Value.Texture);
            Assert.Equal("spark", loaded.Value.Texture!.Name);
            Assert.Equal(8, loaded.Value.BurstCount);

            Result<ParticleEffect> missing = effectDb.Get("sub/missing_tex");
            Assert.False(missing.Success);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ParticleSystem_EmitterAccumulatesAcrossTicksAndTransformsLocalOffsetAndDirection()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(1234));
        ParticleEffect effect = CreateEffect(rate: 10f, lifetimeMin: 2f, lifetimeMax: 2f, speedMin: 4f, speedMax: 4f, spreadDegrees: 0f);

        var store = new DataStore();
        int entity = store.Alloc();
        Quaternion yawLeft = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        store.AddOrUpdate(entity, new TransformComponent(new Vector3(10f, 0f, 0f), yawLeft));
        store.AddOrUpdate(entity, new ParticleEmitterComponent(effect, playing: true, rateScale: 1f)
        {
            Offset = new Vector3(0f, 0f, -1f),
            LocalDirection = new Vector3(0f, 0f, -1f),
        });

        //  0.05s * 10/s = 0.5 accumulated -> 0 particles spawned yet
        system.Tick(0.05f, store);
        Assert.Equal(0, system.ActiveParticleCount);

        //  +0.05s * 10/s = 1.0 -> 1 particle spawned at transformed offset (9, 0, 0) moving along (-1, 0, 0)
        system.Tick(0.05f, store);
        Assert.Equal(1, system.ActiveParticleCount);

        ParticleRenderSnapshot snapshot = system.AcquireRenderSnapshot();
        Assert.Equal(1, snapshot.InstanceCount);
        Assert.Equal(9f, snapshot.Instances[0].Position.X, precision: 3);
        Assert.Equal(0f, snapshot.Instances[0].Position.Y, precision: 3);
        Assert.Equal(0f, snapshot.Instances[0].Position.Z, precision: 3);

        //  Stop emitter; existing particle continues moving along (-1, 0, 0) at 4 u/s
        store.AddOrUpdate(entity, new ParticleEmitterComponent(effect, playing: false));
        system.Tick(0.1f, store);
        Assert.Equal(1, system.ActiveParticleCount);

        snapshot = system.AcquireRenderSnapshot();
        Assert.Equal(8.6f, snapshot.Instances[0].Position.X, precision: 3);
    }

    [Fact]
    public void ParticleSystem_IntegratesGravityDragInterpolationAndExpiration()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        ParticleEffect effect = CreateEffect(
            id: "spark",
            rate: 0f,
            burstCount: 2,
            lifetimeMin: 1f,
            lifetimeMax: 1f,
            speedMin: 10f,
            speedMax: 10f,
            spreadDegrees: 0f,
            startSizeMin: 1f,
            startSizeMax: 1f,
            endSizeMin: 0f,
            endSizeMax: 0f,
            startColor: new Vector4(1f, 0f, 0f, 1f),
            endColor: new Vector4(0f, 1f, 0f, 0f),
            gravity: new Vector3(0f, -10f, 0f),
            drag: 2f
        );
        db.Assets["spark"] = effect;

        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(7));
        var store = new DataStore();

        Assert.False(system.EmitBurst("unknown", Vector3.Zero, Vector3.UnitY).Success);
        Assert.True(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitX, count: 1).Success);

        //  Spawn happens on first Tick; advance is applied to existing particles before spawning new ones
        system.Tick(0.1f, store);
        Assert.Equal(1, system.ActiveParticleCount);

        //  Advance 0.5s (clamped in steps of 0.1s)
        for (var i = 0; i < 5; i++)
        {
            system.Tick(0.1f, store);
        }

        Assert.Equal(1, system.ActiveParticleCount);
        ParticleRenderSnapshot mid = system.AcquireRenderSnapshot();
        Assert.Equal(0.5f, mid.Instances[0].Size, precision: 3);
        Assert.Equal(0.5f, mid.Instances[0].Color.X, precision: 3);
        Assert.Equal(0.5f, mid.Instances[0].Color.Y, precision: 3);
        Assert.True(mid.Instances[0].Position.X > 0f);
        Assert.True(mid.Instances[0].Position.Y < 0f);

        //  Advance past 1.0s lifetime
        for (var i = 0; i < 6; i++)
        {
            system.Tick(0.1f, store);
        }

        Assert.Equal(0, system.ActiveParticleCount);
        ParticleRenderSnapshot expired = system.AcquireRenderSnapshot();
        Assert.Equal(0, expired.InstanceCount);
        Assert.Equal(0, expired.BatchCount);
    }

    [Fact]
    public void ParticleSystem_RespectsPerEmitterAndGlobalMaxCapsAndDisabledSetting()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        var settings = new RenderSettings();
        settings.Particles.MaxParticles.Set(7);

        var system = new ParticleSystem(db, settings, new Random(99));
        ParticleEffect cappedEffect = CreateEffect(id: "capped", maxParticles: 5, burstCount: 10, lifetimeMin: 5f, lifetimeMax: 5f);

        var store = new DataStore();
        system.EmitBurst(new ParticleBurstRequest(cappedEffect, Vector3.Zero, Vector3.UnitY, Count: 10));
        system.Tick(0.016f, store);
        Assert.Equal(5, system.ActiveParticleCount);

        ParticleEffect secondEffect = CreateEffect(id: "second", maxParticles: 5, burstCount: 10, lifetimeMin: 5f, lifetimeMax: 5f);
        system.EmitBurst(new ParticleBurstRequest(secondEffect, Vector3.Zero, Vector3.UnitY, Count: 10));
        system.Tick(0.016f, store);
        Assert.Equal(7, system.ActiveParticleCount);

        settings.Particles.Enabled.Set(false);
        system.Tick(0.016f, store);
        Assert.Equal(0, system.ActiveParticleCount);
    }

    [Fact]
    public void ParticleSystem_GroupsBatchesByTextureAndBlendModeAndSupportsClear()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(42));
        var tex = new Texture("dot", [255, 255, 255, 255], 1, 1, mipmaps: false);

        ParticleEffect solidAdditive = CreateEffect(id: "a", texture: null, blendMode: ParticleBlendMode.Additive, lifetimeMin: 2f, lifetimeMax: 2f);
        ParticleEffect texturedAlpha = CreateEffect(id: "b", texture: tex, blendMode: ParticleBlendMode.Alpha, lifetimeMin: 2f, lifetimeMax: 2f);

        var store = new DataStore();
        system.EmitBurst(new ParticleBurstRequest(solidAdditive, new Vector3(1f, 0f, 0f), Vector3.UnitY, Count: 3));
        system.EmitBurst(new ParticleBurstRequest(texturedAlpha, new Vector3(10f, 0f, 0f), Vector3.UnitY, Count: 2));
        system.EmitBurst(new ParticleBurstRequest(solidAdditive, new Vector3(2f, 0f, 0f), Vector3.UnitY, Count: 1));
        system.Tick(0.016f, store);

        ParticleRenderSnapshot snapshot = system.AcquireRenderSnapshot();
        Assert.Equal(6, snapshot.InstanceCount);
        Assert.Equal(2, snapshot.BatchCount);

        system.Clear();
        system.Tick(0.016f, store);
        Assert.Equal(0, system.ActiveParticleCount);
        Assert.Equal(0, system.AcquireRenderSnapshot().InstanceCount);
    }

    [Fact]
    public void ParticlesShader_ParsesIntoVertexAndFragmentWithInstancedLocationsAndBloomOutput()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("assets"));
        ILogger logger = NullLogger.Instance;
        var parser = new GlslParser(in logger, in vfs);

        Assert.True(vfs.TryGetFile(AssetPaths.Shaders.At("particles.glsl"), out PathInfo shaderPath));
        Shader shader = parser.Parse(shaderPath);

        Assert.Equal(2, shader.Sources.Length);
        ShaderSource vertex = Assert.Single(shader.Sources, s => s.Type == ShaderType.Vertex);
        ShaderSource fragment = Assert.Single(shader.Sources, s => s.Type == ShaderType.Fragment);

        Assert.Contains("layout (location = 8) in vec4 in_particlePosSize;", vertex.Source);
        Assert.Contains("layout (location = 9) in vec4 in_particleColor;", vertex.Source);
        Assert.Contains("layout (location = 10) in vec2 in_particleRotEmissive;", vertex.Source);
        Assert.Contains("BrightColor =", fragment.Source);
    }

    [Fact]
    public void ParticleSystem_IgnoresNonFiniteEmitterAndKeepsValidBatchBounds()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(123));
        ParticleEffect effect = CreateEffect(id: "sparks", rate: 60f, lifetimeMin: 1f, lifetimeMax: 1f, startSizeMin: 0.4f, startSizeMax: 0.4f, endSizeMin: 0.4f, endSizeMax: 0.4f);

        var store = new DataStore();
        int badEntity = store.Alloc();
        store.AddOrUpdate(badEntity, new TransformComponent(new Vector3(float.NaN, 0f, 0f)));
        store.AddOrUpdate(badEntity, new ParticleEmitterComponent(effect, playing: true));

        int goodEntity = store.Alloc();
        store.AddOrUpdate(goodEntity, new TransformComponent(new Vector3(3f, 2f, -1f)));
        store.AddOrUpdate(goodEntity, new ParticleEmitterComponent(effect, playing: true));

        system.Tick(0.05f, store);

        Assert.Equal(3, system.ActiveParticleCount);
        ParticleRenderSnapshot snapshot = system.AcquireRenderSnapshot();
        Assert.Equal(1, snapshot.BatchCount);
        BoundingBox bounds = snapshot.Batches[0].Bounds;
        Assert.True(float.IsFinite(bounds.Center.X) && float.IsFinite(bounds.Extents.X));
        Assert.Equal(3f, bounds.Center.X, precision: 2);
        Assert.True(bounds.Extents.X >= 0.28f);
    }

    [Fact]
    public void ParticleSystem_ClearFollowedByEmitBurstPreservesPostClearBurstAndClearsSnapshotTextures()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(55));
        var texture = new Texture("spark", [255, 255, 255, 255], 1, 1, mipmaps: false);
        ParticleEffect effect = CreateEffect(id: "tex_spark", texture: texture, rate: 0f, burstCount: 2, lifetimeMin: 2f, lifetimeMax: 2f);

        var store = new DataStore();
        system.EmitBurst(new ParticleBurstRequest(effect, new Vector3(1f, 0f, 0f), Vector3.UnitY, Count: 2));
        system.Tick(0.016f, store);
        Assert.Equal(2, system.ActiveParticleCount);

        system.Clear();
        system.EmitBurst(new ParticleBurstRequest(effect, new Vector3(5f, 0f, 0f), Vector3.UnitY, Count: 1));
        system.Tick(0.016f, store);

        Assert.Equal(1, system.ActiveParticleCount);
        ParticleRenderSnapshot snapshot = system.AcquireRenderSnapshot();
        Assert.Equal(1, snapshot.InstanceCount);
        Assert.Equal(5f, snapshot.Instances[0].Position.X, precision: 3);

        snapshot.Reset();
        Assert.Equal(0, snapshot.BatchCount);
        Assert.Null(snapshot.Batches[0].Texture);
    }

    [Fact]
    public void ParticleSystem_RepeatedClearAndPostClearBurstsKeepOnlyLatestGeneration()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(77));
        ParticleEffect burstEffect = CreateEffect(id: "burst_only", rate: 0f, burstCount: 2, lifetimeMin: 2f, lifetimeMax: 2f);
        ParticleEffect emitterEffect = CreateEffect(id: "emitter", rate: 100f, lifetimeMin: 2f, lifetimeMax: 2f);

        var store = new DataStore();
        int emitterEntity = store.Alloc();
        store.AddOrUpdate(emitterEntity, new TransformComponent(new Vector3(-4f, 0f, 0f)));
        store.AddOrUpdate(emitterEntity, new ParticleEmitterComponent(emitterEffect, playing: true));

        system.EmitBurst(new ParticleBurstRequest(burstEffect, new Vector3(1f, 0f, 0f), Vector3.UnitY, Count: 2));
        system.Tick(0.05f, store);
        Assert.True(system.ActiveParticleCount > 2);

        store.AddOrUpdate(emitterEntity, new ParticleEmitterComponent(emitterEffect, playing: false));

        system.EmitBurst(new ParticleBurstRequest(burstEffect, new Vector3(2f, 0f, 0f), Vector3.UnitY, Count: 2));
        system.Clear();
        system.EmitBurst(new ParticleBurstRequest(burstEffect, new Vector3(3f, 0f, 0f), Vector3.UnitY, Count: 2));
        system.Clear();
        system.EmitBurst(new ParticleBurstRequest(burstEffect, new Vector3(8f, 0f, 0f), Vector3.UnitY, Count: 3));

        system.Tick(0.016f, store);
        Assert.Equal(3, system.ActiveParticleCount);
        ParticleRenderSnapshot firstSnapshot = system.AcquireRenderSnapshot();
        Assert.Equal(3, firstSnapshot.InstanceCount);
        for (var i = 0; i < firstSnapshot.InstanceCount; i++)
        {
            Assert.Equal(8f, firstSnapshot.Instances[i].Position.X, precision: 3);
        }

        //  Subsequent tick must keep the surviving post-clear burst alive without re-clearing it.
        system.Tick(0.016f, store);
        Assert.Equal(3, system.ActiveParticleCount);
        ParticleRenderSnapshot secondSnapshot = system.AcquireRenderSnapshot();
        Assert.Equal(3, secondSnapshot.InstanceCount);
    }

    [Fact]
    public void ParticleSystem_DensityScalesEmittersAndBurstsAndClampsBounds()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(123));

        ParticleEffect burstEffect = CreateEffect(id: "burst", rate: 0f, burstCount: 20, maxParticles: 100, lifetimeMin: 2f, lifetimeMax: 2f);
        ParticleEffect emitterEffect = CreateEffect(id: "emitter", rate: 40f, maxParticles: 100, lifetimeMin: 2f, lifetimeMax: 2f);
        var store = new DataStore();

        (float Density, int ExpectedBurst)[] burstCases =
        [
            (0.25f, 5),
            (0.5f, 10),
            (0.75f, 15),
            (1.0f, 20),
            (2.0f, 40),
        ];

        for (var i = 0; i < burstCases.Length; i++)
        {
            system.Clear();
            system.Tick(0.016f, store);

            settings.Particles.Density.Set(burstCases[i].Density);
            system.EmitBurst(new ParticleBurstRequest(burstEffect, Vector3.Zero, Vector3.UnitY, Count: 20));
            system.Tick(0.016f, store);
            Assert.Equal(burstCases[i].ExpectedBurst, system.ActiveParticleCount);
        }

        //  Small positive scaled bursts still emit at least 1 particle while density is positive.
        system.Clear();
        system.Tick(0.016f, store);
        settings.Particles.Density.Set(0.25f);
        system.EmitBurst(new ParticleBurstRequest(burstEffect, Vector3.Zero, Vector3.UnitY, Count: 1));
        system.Tick(0.016f, store);
        Assert.Equal(1, system.ActiveParticleCount);

        //  Zero, negative, and NaN density clamp to 0 and drain pending bursts without spawning.
        float[] zeroDensities = [0f, -2f, float.NaN];
        for (var i = 0; i < zeroDensities.Length; i++)
        {
            system.Clear();
            system.Tick(0.016f, store);

            settings.Particles.Density.Set(zeroDensities[i]);
            system.EmitBurst(new ParticleBurstRequest(burstEffect, Vector3.Zero, Vector3.UnitY, Count: 20));
            system.Tick(0.016f, store);
            Assert.Equal(0, system.ActiveParticleCount);

            //  Raising density later must not replay previously drained zero-density bursts.
            settings.Particles.Density.Set(1.0f);
            system.Tick(0.016f, store);
            Assert.Equal(0, system.ActiveParticleCount);
        }

        //  Emitter rates scale linearly with positive density.
        int emitterEntity = store.Alloc();
        store.AddOrUpdate(emitterEntity, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(emitterEntity, new ParticleEmitterComponent(emitterEffect, playing: true));

        (float Density, int ExpectedEmitter)[] emitterCases =
        [
            (0f, 0),
            (-1f, 0),
            (0.25f, 1),
            (0.5f, 2),
            (0.75f, 3),
            (1.0f, 4),
            (2.0f, 8),
        ];

        for (var i = 0; i < emitterCases.Length; i++)
        {
            system.Clear();
            system.Tick(0.016f, store);

            settings.Particles.Density.Set(emitterCases[i].Density);
            system.Tick(0.1f, store);
            Assert.Equal(emitterCases[i].ExpectedEmitter, system.ActiveParticleCount);
        }

        //  Upper bound (float.MaxValue and PositiveInfinity) clamps cleanly without overflowing.
        float[] highDensities = [float.MaxValue, float.PositiveInfinity];
        for (var i = 0; i < highDensities.Length; i++)
        {
            store.AddOrUpdate(emitterEntity, new ParticleEmitterComponent(emitterEffect, playing: false));
            system.Clear();
            system.Tick(0.016f, store);

            settings.Particles.Density.Set(highDensities[i]);
            system.EmitBurst(new ParticleBurstRequest(burstEffect, Vector3.Zero, Vector3.UnitY, Count: 20));
            system.Tick(0f, store);
            Assert.Equal(100, system.ActiveParticleCount);

            system.Clear();
            system.Tick(0.016f, store);
            store.AddOrUpdate(emitterEntity, new ParticleEmitterComponent(emitterEffect, playing: true));

            //  With dt == 0, even float.MaxValue density must spawn 0 without NaN/Inf corruption.
            system.Tick(0f, store);
            Assert.Equal(0, system.ActiveParticleCount);

            system.Tick(0.016f, store);
            Assert.Equal(100, system.ActiveParticleCount);
        }
    }

    private sealed class CountingAssetDatabase<T> : IAssetDatabase<T>
    {
        public readonly Dictionary<string, T> Assets = new(StringComparer.Ordinal);
        public int GetCalls { get; private set; }

        public Result<T> Get(string id)
        {
            GetCalls++;
            return Assets.TryGetValue(id, out T? value)
                ? Result<T>.FromSuccess(value)
                : Result<T>.FromFailure($"Unknown asset \"{id}\"");
        }
    }

    [Fact]
    public void ParticleEffect_PackTintAndUnpackTintLinearConvertClampAndPreserveUntintedZero()
    {
        Assert.Equal(Vector4.One, ParticleEffect.UnpackTintLinear(0u));

        var srgb = new Vector3(228f / 255f, 59f / 255f, 68f / 255f);
        uint packed = ParticleEffect.PackTint(in srgb, alpha: 0.5f);
        Assert.Equal(0xE43B4480u, packed);

        Vector4 unpacked = ParticleEffect.UnpackTintLinear(packed);
        Assert.Equal(Tonemapper.SrgbToLinear(228f / 255f), unpacked.X, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(59f / 255f), unpacked.Y, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(68f / 255f), unpacked.Z, precision: 4);
        Assert.Equal(128f / 255f, unpacked.W, precision: 4);

        uint pureBlack = ParticleEffect.PackTint(Vector3.Zero, alpha: 1f);
        Assert.Equal(0x000000FFu, pureBlack);
        Vector4 unpackedBlack = ParticleEffect.UnpackTintLinear(pureBlack);
        Assert.Equal(0f, unpackedBlack.X, precision: 5);
        Assert.Equal(0f, unpackedBlack.Y, precision: 5);
        Assert.Equal(0f, unpackedBlack.Z, precision: 5);
        Assert.Equal(1f, unpackedBlack.W, precision: 5);

        uint clamped = ParticleEffect.PackTint(new Vector3(float.NaN, 2.5f, -1f), alpha: float.PositiveInfinity);
        Assert.Equal(0x00FF0000u, clamped);
    }

    [Fact]
    public void ParticleSystem_ResolvesEffectIdOnceCachesAndSkipsUnknownIdsGracefully()
    {
        var db = new CountingAssetDatabase<ParticleEffect>();
        ParticleEffect effect = CreateEffect(id: "thruster", rate: 100f, lifetimeMin: 1f, lifetimeMax: 1f);
        db.Assets["thruster"] = effect;

        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(7), NullLogger<ParticleSystem>.Instance);
        var store = new DataStore();

        int validEntity = store.Alloc();
        store.AddOrUpdate(validEntity, new TransformComponent(new Vector3(1f, 2f, 3f)));
        store.AddOrUpdate(validEntity, new ParticleEmitterComponent("thruster", playing: true));

        int missingEntity = store.Alloc();
        store.AddOrUpdate(missingEntity, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(missingEntity, new ParticleEmitterComponent("missing_mod_effect", playing: true));

        system.Tick(0.05f, store);
        Assert.Equal(5, system.ActiveParticleCount);
        Assert.Equal(2, db.GetCalls);

        //  Subsequent ticks hit the cached valid and missing entries instead of re-querying the database.
        system.Tick(0.05f, store);
        Assert.Equal(10, system.ActiveParticleCount);
        Assert.Equal(2, db.GetCalls);
    }

    [Fact]
    public void ParticleSystem_MutedComponentPausesSpawnWithoutMutatingPlayingAndResumesWhenRemoved()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        ParticleEffect effect = CreateEffect(id: "spray", rate: 100f, lifetimeMin: 0.25f, lifetimeMax: 0.25f);
        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(11));
        var store = new DataStore();

        int entity = store.Alloc();
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(entity, new ParticleEmitterComponent(effect, playing: true));

        system.Tick(0.05f, store);
        Assert.Equal(5, system.ActiveParticleCount);

        store.AddOrUpdate(entity, new ParticleEmitterMutedComponent());
        system.Tick(0.05f, store);
        Assert.Equal(5, system.ActiveParticleCount);
        Assert.True(store.TryGet(entity, out ParticleEmitterComponent emitter) && emitter.Playing);

        //  Drain live particles completely while muted.
        system.Tick(0.1f, store);
        system.Tick(0.1f, store);
        system.Tick(0.1f, store);
        Assert.Equal(0, system.ActiveParticleCount);

        //  Unmuting resumes immediately with a zeroed accumulator.
        store.Remove(typeof(ParticleEmitterMutedComponent), entity);
        system.Tick(0.05f, store);
        Assert.Equal(5, system.ActiveParticleCount);
    }

    [Fact]
    public void ParticleSystem_TintRgbaModulatesEmitterAndBurstColorsInLinearSpace()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        ParticleEffect whiteEffect = CreateEffect(
            id: "paint",
            rate: 100f,
            burstCount: 3,
            lifetimeMin: 1f,
            lifetimeMax: 1f,
            startColor: Vector4.One,
            secondaryStartColor: null,
            endColor: new Vector4(1f, 1f, 1f, 0f)
        );
        db.Assets["paint"] = whiteEffect;

        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(19));
        var store = new DataStore();

        uint tint = ParticleEffect.PackTint(new Vector3(0.8f, 0.2f, 0.1f), alpha: 0.5f);
        Vector4 expectedLinear = ParticleEffect.UnpackTintLinear(tint);

        int emitterEntity = store.Alloc();
        store.AddOrUpdate(emitterEntity, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(emitterEntity, new ParticleEmitterComponent("paint", playing: true, tintRgba: tint));

        system.Tick(0.01f, store);
        ParticleRenderSnapshot emitterSnapshot = system.AcquireRenderSnapshot();
        Assert.Equal(1, emitterSnapshot.InstanceCount);
        Vector4 emittedColor = emitterSnapshot.Instances[0].Color;
        Assert.Equal(expectedLinear.X, emittedColor.X, precision: 4);
        Assert.Equal(expectedLinear.Y, emittedColor.Y, precision: 4);
        Assert.Equal(expectedLinear.Z, emittedColor.Z, precision: 4);
        Assert.Equal(expectedLinear.W, emittedColor.W, precision: 4);

        //  Clear emitter, then verify untinted burst and 7-param tinted burst by effectId.
        store.Remove(typeof(ParticleEmitterComponent), emitterEntity);
        system.Clear();
        system.Tick(0.016f, store);

        uint blackTint = ParticleEffect.PackTint(Vector3.Zero, alpha: 1f);
        Result burstResult = system.EmitBurst("paint", Vector3.Zero, Vector3.UnitY, count: 2, speedScale: 1f, sizeScale: 1f, tintRgba: blackTint);
        Assert.True(burstResult.Success);
        system.Tick(0.016f, store);

        ParticleRenderSnapshot blackSnapshot = system.AcquireRenderSnapshot();
        Assert.Equal(2, blackSnapshot.InstanceCount);
        for (var i = 0; i < blackSnapshot.InstanceCount; i++)
        {
            Assert.Equal(0f, blackSnapshot.Instances[i].Color.X, precision: 5);
            Assert.Equal(0f, blackSnapshot.Instances[i].Color.Y, precision: 5);
            Assert.Equal(0f, blackSnapshot.Instances[i].Color.Z, precision: 5);
        }

        //  Untinted (0u) keeps white StartColor intact.
        system.Clear();
        system.Tick(0.016f, store);
        system.EmitBurst(new ParticleBurstRequest(whiteEffect, Vector3.Zero, Vector3.UnitY, Count: 1, TintRgba: 0u));
        system.Tick(0.016f, store);
        ParticleRenderSnapshot untintedSnapshot = system.AcquireRenderSnapshot();
        Assert.Equal(1, untintedSnapshot.InstanceCount);
        Assert.Equal(1f, untintedSnapshot.Instances[0].Color.X, precision: 4);
        Assert.Equal(1f, untintedSnapshot.Instances[0].Color.Y, precision: 4);
        Assert.Equal(1f, untintedSnapshot.Instances[0].Color.Z, precision: 4);
    }

    [Fact]
    public void ParticleSystem_EmitBurstByEffectIdRejectsInvalidInputsWithFailure()
    {
        var db = new DictionaryAssetDatabase<ParticleEffect>();
        db.Assets["spark"] = CreateEffect("spark", rate: 0f, burstCount: 2);

        var settings = new RenderSettings();
        var system = new ParticleSystem(db, settings, new Random(19));
        var store = new DataStore();

        Assert.False(system.EmitBurst("spark", new Vector3(float.NaN, 0f, 0f), Vector3.UnitY).Success);
        Assert.False(system.EmitBurst("spark", new Vector3(0f, float.PositiveInfinity, 0f), Vector3.UnitY).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, new Vector3(0f, float.NaN, 0f)).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, new Vector3(float.NegativeInfinity, 0f, 0f)).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: null, speedScale: -0.1f, sizeScale: 1f).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: null, speedScale: float.NaN, sizeScale: 1f).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: null, speedScale: 1f, sizeScale: 0f).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: null, speedScale: 1f, sizeScale: -1f).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: null, speedScale: 1f, sizeScale: float.NaN).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: 0, speedScale: 1f, sizeScale: 1f).Success);
        Assert.False(system.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: -1, speedScale: 1f, sizeScale: 1f).Success);

        system.Tick(0.016f, store);
        Assert.Equal(0, system.ActiveParticleCount);
    }
}
