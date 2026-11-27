#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Tomlet;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Server.Generation;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Verifies headless bundled blueprint loading and Adventure starter wrecked lifeboat generation.</summary>
public class AdventureStarterWreckTests
{
    private const string WRECKED_LIFEBOAT_ID = "builtin/wrecked_lifeboat";

    [Fact]
    public void BundledWreckedLifeboatLoadsHeadlesslyFromServerAssetsWithoutClient()
    {
        string serverAssets = GetServerAssetsRoot();
        string blueprintFile = Path.Combine(serverAssets, "blueprints", "builtin", "wrecked_lifeboat.wbp");
        Assert.True(File.Exists(blueprintFile), $"Expected bundled wrecked lifeboat blueprint at {blueprintFile}");

        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(serverAssets));

        using var di = new Container();
        di.RegisterInstance<ILogger<VirtualFileParseService>>(NullLogger<VirtualFileParseService>.Instance);
        di.RegisterInstance<ILogger<BundledBlueprintDatabase>>(NullLogger<BundledBlueprintDatabase>.Instance);
        di.RegisterInstance(vfs);
        di.Register<IFileParseService, VirtualFileParseService>(Reuse.Singleton);
        di.RegisterBundledBlueprints();

        IBundledBlueprintDatabase bundled = di.Resolve<IBundledBlueprintDatabase>();
        Assert.Contains(bundled.ListSummaries(), s => s.Id == WRECKED_LIFEBOAT_ID && s.BrickCount > 0);

        Result<BlueprintData> loaded = bundled.Get(WRECKED_LIFEBOAT_ID);
        Assert.True(loaded.Success, loaded.Message);

        BlueprintData raw = loaded.Value;
        Assert.True(raw.SizeX > 0 && raw.SizeY > 0 && raw.SizeZ > 0);
        Assert.NotNull(raw.BrickPalette);
        Assert.NotEmpty(raw.BrickPalette!);
        Assert.NotNull(raw.PaintPalette);
        Assert.NotEmpty(raw.PaintPalette!);

        string[] shippedBricks = LoadShippedBrickIds();
        string[] shippedPaints = LoadShippedPaintIds();
        var brickSet = new HashSet<string>(shippedBricks, StringComparer.Ordinal);
        var paintSet = new HashSet<string>(shippedPaints, StringComparer.Ordinal);

        foreach (string brickName in raw.BrickPalette!)
        {
            if (!string.IsNullOrEmpty(brickName))
            {
                Assert.Contains(brickName, brickSet);
            }
        }

        foreach (string paintName in raw.PaintPalette!)
        {
            if (!string.IsNullOrEmpty(paintName))
            {
                Assert.Contains(paintName, paintSet);
            }
        }

        var brickRegistry = BrickRegistry.FromNames(shippedBricks);
        var paintRegistry = PaintRegistry.FromNames(shippedPaints);

        VoxelRegionData encoded = raw.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, brickRegistry, paintRegistry);
        VoxelObject centered = VoxelRegionApplier.ToCenteredVoxelObject(in decoded, out _);

        int solidCount = 0;
        int overrideCount = 0;
        foreach (ChunkData chunk in centered)
        {
            foreach (VoxelSample sample in chunk.GetOccupiedSampler())
            {
                if (sample.Center.ID == 0)
                {
                    continue;
                }

                solidCount++;
                Assert.InRange(sample.Center.ID, (ushort)1, (ushort)brickRegistry.Count);
                if (sample.Center.HasFaceOverrides())
                {
                    overrideCount++;
                }
            }
        }

        Assert.True(solidCount > 0);
        Assert.Equal(raw.BrickCount, solidCount);
        Assert.True(overrideCount > 0);
    }

    [Fact]
    public void AdventureGenerationSpawnsDeterministicWreckWithPalettesAndCreativeOmitsIt()
    {
        IBundledBlueprintDatabase bundled = CreateServerBundledBlueprintDatabase();
        var brickRegistry = BrickRegistry.FromNames(LoadShippedBrickIds());
        var paintRegistry = PaintRegistry.FromNames(LoadShippedPaintIds());
        LevelGeneratorConfig config = CreateSmallConfig();
        var spawn = new Vector3(0f, 1f, 5f);

        var advGenA = new LevelGenerator(seed: 123456789L, brickRegistry, config, paintRegistry, bundled, NullLogger.Instance);
        var advGenB = new LevelGenerator(seed: 123456789L, brickRegistry, config, paintRegistry, bundled, NullLogger.Instance);
        var creativeGen = new LevelGenerator(seed: 123456789L, brickRegistry, config, paintRegistry, bundled, NullLogger.Instance);

        VoxelEntityData[] adventureA = advGenA.Generate(GameMode.Adventure, spawn);
        VoxelEntityData[] adventureB = advGenB.Generate(GameMode.Adventure, spawn);
        VoxelEntityData[] creative = creativeGen.Generate(GameMode.Creative, spawn);

        ulong expectedWreckUuid = advGenA.GenerateStarterWreckUuid().ToValue();
        Assert.NotEqual(0UL, expectedWreckUuid);

        Assert.Equal(config.AsteroidCount.Get() + 1, adventureA.Length);
        Assert.Equal(config.AsteroidCount.Get() + 1, adventureB.Length);
        Assert.Equal(config.AsteroidCount.Get(), creative.Length);
        Assert.DoesNotContain(creative, e => e.Uuid == expectedWreckUuid);

        VoxelEntityData wreckA = Assert.Single(adventureA, e => e.Uuid == expectedWreckUuid);
        VoxelEntityData wreckB = Assert.Single(adventureB, e => e.Uuid == expectedWreckUuid);

        //  Deterministic placement, orientation, UUID, and palettes.
        Assert.Equal(wreckA.Uuid, wreckB.Uuid);
        Assert.Equal(wreckA.X, wreckB.X);
        Assert.Equal(wreckA.Y, wreckB.Y);
        Assert.Equal(wreckA.Z, wreckB.Z);
        Assert.Equal(wreckA.OrientationX, wreckB.OrientationX);
        Assert.Equal(wreckA.OrientationY, wreckB.OrientationY);
        Assert.Equal(wreckA.OrientationZ, wreckB.OrientationZ);
        Assert.Equal(wreckA.OrientationW, wreckB.OrientationW);
        Assert.Equal(wreckA.BrickPalette, wreckB.BrickPalette);
        Assert.Equal(wreckA.PaintPalette, wreckB.PaintPalette);

        //  No UUID collisions across all generated entities.
        Assert.Equal(adventureA.Length, adventureA.Select(e => e.Uuid).Distinct().Count());

        //  Portable palettes decode cleanly into a differently-ordered registry.
        Assert.NotNull(wreckA.BrickPalette);
        Assert.NotEmpty(wreckA.BrickPalette!);
        Assert.NotNull(wreckA.PaintPalette);
        Assert.NotEmpty(wreckA.PaintPalette!);

        var altBricks = BrickRegistry.FromNames(LoadShippedBrickIds().Concat(["wb:zzz_extra_brick"]));
        var altPaints = PaintRegistry.FromNames(LoadShippedPaintIds().Concat(["wb:zzz_extra_paint"]));
        Result<VoxelEntityData> decoded = VoxelCodec.Decode(in wreckA, altBricks, altPaints);
        Assert.True(decoded.Success, decoded.Message);
        Assert.Null(decoded.Value.BrickPalette);
        Assert.Null(decoded.Value.PaintPalette);

        var rebuilt = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE, decoded.Value.Chunks);
        Assert.True(VoxelRegionApplier.HasSolidVoxels(rebuilt));
    }

    [Fact]
    public void AdventureGenerationGuaranteesPlayerSpawnAndAsteroidClearanceAcrossSeeds()
    {
        IBundledBlueprintDatabase bundled = CreateServerBundledBlueprintDatabase();
        var brickRegistry = BrickRegistry.FromNames(LoadShippedBrickIds());
        var paintRegistry = PaintRegistry.FromNames(LoadShippedPaintIds());
        var config = new LevelGeneratorConfig();
        config.AsteroidCount.Set(12);
        config.AsteroidMinRadius.Set(8);
        config.AsteroidMaxRadius.Set(18);
        config.WorldSpan.Set(140);
        config.WorldHeight.Set(60);

        var spawn = new Vector3(0f, 1f, 5f);
        long[] seeds = [0L, 1L, 42L, -999999L, 77777777777L, long.MaxValue];

        foreach (long seed in seeds)
        {
            var generator = new LevelGenerator(seed, brickRegistry, config, paintRegistry, bundled, NullLogger.Instance);
            VoxelEntityData[] entities = generator.Generate(GameMode.Adventure, spawn);

            ulong wreckUuid = generator.GenerateStarterWreckUuid().ToValue();
            Assert.NotEqual(0UL, wreckUuid);
            VoxelEntityData wreck = Assert.Single(entities, e => e.Uuid == wreckUuid);

            Result<VoxelEntityData> decodedWreck = VoxelCodec.Decode(in wreck, brickRegistry, paintRegistry);
            Assert.True(decodedWreck.Success, decodedWreck.Message);
            var wreckObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE, decodedWreck.Value.Chunks);
            float wreckRadius = LevelGenerator.ComputeBoundingRadius(wreckObject);

            var wreckPos = new Vector3((float)wreck.X, (float)wreck.Y, (float)wreck.Z);
            float spawnDistance = Vector3.Distance(spawn, wreckPos);
            Assert.True(
                spawnDistance >= wreckRadius + 2.0f,
                $"Seed {seed}: spawn distance {spawnDistance:F2} too close to wreck bounding radius {wreckRadius:F2}.");

            //  Verify no asteroid overlaps the player spawn clearance or the starter wreck bounding sphere.
            foreach (VoxelEntityData entity in entities)
            {
                if (entity.Uuid == wreckUuid)
                {
                    continue;
                }

                Result<VoxelEntityData> decodedAsteroid = VoxelCodec.Decode(in entity, brickRegistry, paintRegistry);
                Assert.True(decodedAsteroid.Success, decodedAsteroid.Message);
                var asteroidObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE, decodedAsteroid.Value.Chunks);
                float asteroidRadius = LevelGenerator.ComputeBoundingRadius(asteroidObject);
                var asteroidPos = new Vector3((float)entity.X, (float)entity.Y, (float)entity.Z);

                float distToSpawn = Vector3.Distance(spawn, asteroidPos);
                Assert.True(
                    distToSpawn > asteroidRadius + 2.0f,
                    $"Seed {seed}: asteroid {entity.Uuid} at dist {distToSpawn:F2} overlaps spawn (radius {asteroidRadius:F2}).");

                float distToWreck = Vector3.Distance(wreckPos, asteroidPos);
                Assert.True(
                    distToWreck > wreckRadius + asteroidRadius,
                    $"Seed {seed}: asteroid {entity.Uuid} at dist {distToWreck:F2} overlaps wreck (combined radii {wreckRadius + asteroidRadius:F2}).");
            }
        }
    }

    [Fact]
    public void AdventureGenerationHandlesMissingOrUnreadableBlueprintCleanly()
    {
        var brickRegistry = BrickRegistry.FromNames(LoadShippedBrickIds());
        var paintRegistry = PaintRegistry.FromNames(LoadShippedPaintIds());
        LevelGeneratorConfig config = CreateSmallConfig();
        var spawn = new Vector3(0f, 1f, 5f);

        //  1. Missing blueprint database
        var loggerNullDb = new RecordingLogger<LevelGenerator>();
        VoxelEntityData[] withoutDb = new LevelGenerator(7, brickRegistry, config, paintRegistry, bundledBlueprints: null, loggerNullDb)
            .Generate(GameMode.Adventure, spawn);
        Assert.Equal(config.AsteroidCount.Get(), withoutDb.Length);
        Assert.Contains(loggerNullDb.Entries, e => e.Level == LogLevel.Warning);

        //  2. Missing blueprint ID
        var missingConfig = CreateSmallConfig();
        missingConfig.StarterWreck.BlueprintId.Set("builtin/missing_lifeboat");
        var loggerMissing = new RecordingLogger<LevelGenerator>();
        VoxelEntityData[] withMissing = new LevelGenerator(7, brickRegistry, missingConfig, paintRegistry, CreateServerBundledBlueprintDatabase(), loggerMissing)
            .Generate(GameMode.Adventure, spawn);
        Assert.Equal(missingConfig.AsteroidCount.Get(), withMissing.Length);
        Assert.Contains(loggerMissing.Entries, e => e.Level == LogLevel.Warning);

        //  3. Invalid / empty blueprint payload
        var corruptDb = new StubBundledBlueprintDatabase(new BlueprintData
        {
            Name = "Corrupt",
            SizeX = 0,
            SizeY = 0,
            SizeZ = 0,
            Chunks = [],
        });
        var loggerCorrupt = new RecordingLogger<LevelGenerator>();
        VoxelEntityData[] withCorrupt = new LevelGenerator(7, brickRegistry, config, paintRegistry, corruptDb, loggerCorrupt)
            .Generate(GameMode.Adventure, spawn);
        Assert.Equal(config.AsteroidCount.Get(), withCorrupt.Length);
        Assert.Contains(loggerCorrupt.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void SqliteLevelCatalogPersistsLifeboatInAdventureAndOmitsInCreative()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wb-levelcat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(tempRoot));
            var storagePaths = new StoragePaths(new StorageConfig(), appPaths);
            var brickRegistry = BrickRegistry.FromNames(LoadShippedBrickIds());
            var paintRegistry = PaintRegistry.FromNames(LoadShippedPaintIds());
            var gameplayConfig = new GameplayConfig();
            LevelGeneratorConfig levelGenConfig = CreateSmallConfig();
            var migrator = new Migrator([]);
            var seedPhraseService = CreateSeedPhraseService();
            IBundledBlueprintDatabase bundled = CreateServerBundledBlueprintDatabase();

            var catalog = new SqliteLevelCatalog(
                NullLogger<SqliteLevelCatalog>.Instance,
                storagePaths,
                brickRegistry,
                NullLoggerFactory.Instance,
                migrator,
                gameplayConfig,
                levelGenConfig,
                seedPhraseService,
                paintRegistry,
                bundled);

            Assert.True(catalog.Create("Adventure World", "test-seed-915", GameMode.Adventure, out string adventureGuid));
            Assert.True(catalog.Create("Creative World", "test-seed-915", GameMode.Creative, out string creativeGuid));

            using ILevelStorage? advStore = catalog.Open(adventureGuid);
            Assert.NotNull(advStore);
            Result<Level> advLevel = advStore!.GetLevel();
            Assert.True(advLevel.Success, advLevel.Message);
            Assert.Equal(GameMode.Adventure, advLevel.Value.DefaultGameMode);

            VoxelEntityData[] advEntities = advStore.GetVoxelEntities(advLevel.Value.Version.DataVersion).ToArray();
            Assert.Equal(levelGenConfig.AsteroidCount.Get() + 1, advEntities.Length);

            var expectedGen = new LevelGenerator(advLevel.Value.Seed, brickRegistry, levelGenConfig, paintRegistry, bundled);
            ulong expectedWreckUuid = expectedGen.GenerateStarterWreckUuid().ToValue();
            VoxelEntityData persistedWreck = Assert.Single(advEntities, e => e.Uuid == expectedWreckUuid);
            Assert.NotNull(persistedWreck.BrickPalette);
            Assert.NotEmpty(persistedWreck.BrickPalette!);
            Assert.NotNull(persistedWreck.PaintPalette);
            Assert.NotEmpty(persistedWreck.PaintPalette!);

            Result<VoxelEntityData> decoded = VoxelCodec.Decode(in persistedWreck, brickRegistry, paintRegistry);
            Assert.True(decoded.Success, decoded.Message);

            var store = new DataStore();
            Entity authorityEntity = VoxelWorldEntityFactory.CreateAuthority(store, decoded.Value);
            Assert.Equal(expectedWreckUuid, authorityEntity.Uuid.ToValue());

            using ILevelStorage? creativeStore = catalog.Open(creativeGuid);
            Assert.NotNull(creativeStore);
            Result<Level> creativeLevel = creativeStore!.GetLevel();
            Assert.True(creativeLevel.Success, creativeLevel.Message);
            VoxelEntityData[] creativeEntities = creativeStore.GetVoxelEntities(creativeLevel.Value.Version.DataVersion).ToArray();
            Assert.Equal(levelGenConfig.AsteroidCount.Get(), creativeEntities.Length);
            Assert.DoesNotContain(creativeEntities, e => e.Uuid == expectedWreckUuid);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static LevelGeneratorConfig CreateSmallConfig()
    {
        var config = new LevelGeneratorConfig();
        config.AsteroidCount.Set(4);
        config.AsteroidMinRadius.Set(6);
        config.AsteroidMaxRadius.Set(10);
        config.WorldSpan.Set(120);
        config.WorldHeight.Set(50);
        return config;
    }

    private static IBundledBlueprintDatabase CreateServerBundledBlueprintDatabase()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(GetServerAssetsRoot()));
        var parseService = new VirtualFileParseService([new BlueprintDataParser()], vfs);
        return new BundledBlueprintDatabase(
            NullLogger<BundledBlueprintDatabase>.Instance,
            parseService,
            vfs);
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string GetServerAssetsRoot()
    {
        return Path.Combine(GetRepoRoot(), "WaywardBeyond.Server", "assets");
    }

    private static string[] LoadShippedBrickIds()
    {
        string bricksDir = Path.Combine(GetRepoRoot(), "WaywardBeyond.Bricks", "assets", "bricks");
        var ids = new List<string>();
        foreach (string file in Directory.EnumerateFiles(bricksDir, "*.toml", SearchOption.AllDirectories))
        {
            BrickDefinitions defs = TomletMain.To<BrickDefinitions>(File.ReadAllText(file));
            if (defs.Bricks == null)
            {
                continue;
            }

            foreach (BrickDefinition def in defs.Bricks)
            {
                if (!string.IsNullOrWhiteSpace(def.ID))
                {
                    ids.Add(def.ID);
                }
            }
        }

        return [.. ids];
    }

    private static string[] LoadShippedPaintIds()
    {
        string paintsDir = Path.Combine(GetRepoRoot(), "WaywardBeyond.Bricks", "assets", "paints");
        var ids = new List<string>();
        foreach (string file in Directory.EnumerateFiles(paintsDir, "*.toml", SearchOption.AllDirectories))
        {
            PaintDefinitions defs = TomletMain.To<PaintDefinitions>(File.ReadAllText(file));
            if (defs.Paints == null)
            {
                continue;
            }

            foreach (PaintDefinition def in defs.Paints)
            {
                if (!string.IsNullOrWhiteSpace(def.ID))
                {
                    ids.Add(def.ID);
                }
            }
        }

        return [.. ids];
    }

    private static SeedPhraseService CreateSeedPhraseService()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("assets"));
        var parseService = new VirtualFileParseService([new WaywardBeyond.Data.Localization.LocalizedTagDefinitionParser()], vfs);
        var tags = new WaywardBeyond.Data.Localization.LocalizedTagsDatabase(
            NullLogger<WaywardBeyond.Data.Localization.LocalizedTagsDatabase>.Instance,
            parseService,
            vfs);
        return new SeedPhraseService(tags);
    }

    private sealed class StubBundledBlueprintDatabase(BlueprintData data) : IBundledBlueprintDatabase
    {
        public Result<BlueprintData> Get(string id) => Result<BlueprintData>.FromSuccess(data);

        public IReadOnlyList<BlueprintSummary> ListSummaries() => [];
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public readonly List<(LogLevel Level, string Message)> Entries = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
