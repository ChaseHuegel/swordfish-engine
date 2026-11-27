using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Generation;

using AsteroidStructure = (Vector3 Position, int Radius);
using ClearanceZone = (Vector3 Position, float Radius);

/// <summary>
///     Deterministic level generation: produces the fixed set of asteroid structures (and Adventure
///     starter wreckage) for a seed as serialized <see cref="VoxelEntityData"/>. Rendered/server-agnostic -
///     it neither constructs ECS entities nor meshes. The server runs it to author a new world, and the
///     same seed derives identical data, so a level can be regenerated.
/// </summary>
public sealed class LevelGenerator(
    in long seed,
    IBrickRegistry brickRegistry,
    LevelGeneratorConfig config,
    IPaintRegistry? paintRegistry = null,
    IBundledBlueprintDatabase? bundledBlueprints = null,
    ILogger? logger = null
)
{
    private readonly long _seed = seed;
    private readonly int _foldedSeed = FoldSeed(seed);
    private readonly Randomizer _randomizer = new(FoldSeed(seed));
    private readonly AsteroidGenerator _asteroidGenerator = new(brickRegistry, FoldSeed(seed), config.Asteroid);
    private readonly IBrickRegistry _brickRegistry = brickRegistry;
    private readonly IPaintRegistry? _paintRegistry = paintRegistry;
    private readonly IBundledBlueprintDatabase? _bundledBlueprints = bundledBlueprints;
    private readonly ILogger? _logger = logger;

    //  Placement is rejection-sampled: a dense radius or a tight volume can make every candidate
    //  intersect, so cap the work instead of spinning on the server thread.
    private const int MAX_PLACEMENT_ATTEMPTS_PER_ASTEROID = 100;

    //  High bit separates starter structure UUIDs from sequential asteroid indices (1..AsteroidCount).
    private const uint STARTER_WRECK_UUID_TAG = 0x8000_0001u;
    private const int STARTER_WRECK_SEED_SALT = 0x5A3C_9E17;

    //  Minimum clearance radius around LevelSpawn so the player never spawns inside starter colliders.
    private const float MIN_PLAYER_CLEARANCE_RADIUS = 3.5f;

    //  Forward placement cone (centered on -Z) and gentle wrecked tilt ranges in radians.
    private const float WRECK_YAW_ARC_RADIANS = 0.6f;
    private const float WRECK_PITCH_ARC_RADIANS = 0.2f;
    private const float WRECK_TILT_MAX_RADIANS = 0.45f;

    /// <summary>Generates the level structures for the seed and game mode. Throws when asteroid placement cannot complete.</summary>
    public VoxelEntityData[] Generate(GameMode gameMode = GameMode.Creative, Vector3? spawnPosition = null)
    {
        var clearanceZones = new List<ClearanceZone>(2);
        VoxelEntityData? starterWreck = null;

        if (gameMode == GameMode.Adventure)
        {
            Vector3 spawn = spawnPosition ?? Vector3.Zero;
            float spawnClearance = MathF.Max(config.StarterWreck.SpawnClearanceRadius.Get(), MIN_PLAYER_CLEARANCE_RADIUS);
            clearanceZones.Add((spawn, spawnClearance));

            Result<StarterWreckPlacement> wreckResult = TryBuildStarterWreck(spawn);
            if (wreckResult.Success)
            {
                StarterWreckPlacement wreck = wreckResult.Value;
                starterWreck = wreck.Entity;
                float wreckClearance = wreck.BoundingRadius + config.StarterWreck.AsteroidClearancePadding.Get();
                clearanceZones.Add((wreck.Position, wreckClearance));
            }
            else
            {
                _logger?.LogWarning(
                    wreckResult.Exception,
                    "Skipping Adventure starter wreck for seed {Seed}: {Message}",
                    _seed,
                    wreckResult.Message);
            }
        }

        int asteroidCount = config.AsteroidCount;
        int asteroidMinRadius = config.AsteroidMinRadius;
        int asteroidMaxRadius = config.AsteroidMaxRadius;

        int worldHeight = config.WorldHeight;
        int worldSpan = config.WorldSpan;

        var asteroids = new List<AsteroidStructure>(asteroidCount);
        int attempts = 0;
        int maxAttempts = asteroidCount * MAX_PLACEMENT_ATTEMPTS_PER_ASTEROID;
        while (asteroids.Count < asteroidCount)
        {
            if (++attempts > maxAttempts)
            {
                throw new InvalidOperationException(
                    $"Could not place {asteroidCount} asteroids in the configured volume after {maxAttempts} attempts; " +
                    "reduce LevelGenerator.AsteroidCount or the asteroid radii, or widen the world span.");
            }

            var position = new Vector3
            (
                _randomizer.NextInt(-worldSpan, worldSpan),
                _randomizer.NextInt(-worldHeight, worldHeight),
                _randomizer.NextInt(-worldSpan, worldSpan)
            );

            int radius = _randomizer.NextInt(asteroidMinRadius, asteroidMaxRadius + 1);

            if (IntersectsClearanceZones(clearanceZones, position, radius) || IntersectsAsteroids(asteroids, position, radius))
            {
                continue;
            }

            asteroids.Add(new AsteroidStructure(position, radius));
        }

        int totalCount = asteroids.Count + (starterWreck.HasValue ? 1 : 0);
        var generated = new VoxelEntityData[totalCount];
        for (var i = 0; i < asteroids.Count; i++)
        {
            AsteroidStructure asteroid = asteroids[i];
            Uuid uuid = GenerateUuid(i);
            generated[i] = _asteroidGenerator.GenerateAt(asteroid.Position, diameter: asteroid.Radius * 2, uuid);
        }

        if (starterWreck.HasValue)
        {
            generated[asteroids.Count] = starterWreck.Value;
        }

        return generated;
    }

    private Result<StarterWreckPlacement> TryBuildStarterWreck(Vector3 spawn)
    {
        string blueprintId = config.StarterWreck.BlueprintId.Get();
        if (string.IsNullOrWhiteSpace(blueprintId))
        {
            return Result<StarterWreckPlacement>.FromFailure("Starter wreck blueprint ID is empty.");
        }

        if (_bundledBlueprints == null)
        {
            return Result<StarterWreckPlacement>.FromFailure(
                $"Bundled blueprint database is unavailable for \"{blueprintId}\".");
        }

        try
        {
            Result<BlueprintData> loaded = _bundledBlueprints.Get(blueprintId);
            if (!loaded.Success)
            {
                return new Result<StarterWreckPlacement>(
                    false,
                    default,
                    $"Failed to load starter wreck blueprint \"{blueprintId}\": {loaded.Message}",
                    loaded.Exception);
            }

            BlueprintData blueprint = loaded.Value;
            if (blueprint.SizeX <= 0 || blueprint.SizeY <= 0 || blueprint.SizeZ <= 0 || blueprint.Chunks == null)
            {
                return Result<StarterWreckPlacement>.FromFailure(
                    $"Starter wreck blueprint \"{blueprintId}\" has invalid bounds.");
            }

            VoxelRegionData encodedRegion = blueprint.ToRegion();
            VoxelRegionData decodedRegion = VoxelCodec.Decode(in encodedRegion, _brickRegistry, _paintRegistry);
            VoxelObject voxelObject = VoxelRegionApplier.ToCenteredVoxelObject(in decodedRegion, out _);
            if (!VoxelRegionApplier.HasSolidVoxels(voxelObject))
            {
                return Result<StarterWreckPlacement>.FromFailure(
                    $"Starter wreck blueprint \"{blueprintId}\" contains no solid voxels.");
            }

            float boundingRadius = ComputeBoundingRadius(voxelObject);
            var wreckRandom = new Randomizer(_foldedSeed ^ STARTER_WRECK_SEED_SALT);

            float minDistance = MathF.Max(
                config.StarterWreck.SpawnDistanceMin.Get(),
                boundingRadius + MIN_PLAYER_CLEARANCE_RADIUS);
            float maxDistance = MathF.Max(minDistance, config.StarterWreck.SpawnDistanceMax.Get());
            float distance = minDistance + wreckRandom.NextFloat() * (maxDistance - minDistance);

            float azimuth = (wreckRandom.NextFloat() * 2f - 1f) * WRECK_YAW_ARC_RADIANS;
            float elevation = (wreckRandom.NextFloat() * 2f - 1f) * WRECK_PITCH_ARC_RADIANS;
            float cosElev = MathF.Cos(elevation);
            var direction = new Vector3(
                MathF.Sin(azimuth) * cosElev,
                MathF.Sin(elevation),
                -MathF.Cos(azimuth) * cosElev);
            Vector3 position = spawn + direction * distance;

            float yaw = wreckRandom.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
            float pitch = (wreckRandom.NextFloat() * 2f - 1f) * WRECK_TILT_MAX_RADIANS;
            float roll = (wreckRandom.NextFloat() * 2f - 1f) * WRECK_TILT_MAX_RADIANS;
            Quaternion orientation = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll));

            Uuid uuid = GenerateStarterWreckUuid();
            var rawEntity = new VoxelEntityData(
                uuid.ToValue(),
                position.X, position.Y, position.Z,
                orientation.X, orientation.Y, orientation.Z, orientation.W,
                _ScaleX: 1, _ScaleY: 1, _ScaleZ: 1,
                voxelObject.GetChunkInfos(),
                _BrickPalette: null,
                _PaintPalette: decodedRegion.PaintPalette
            );

            Result<VoxelEntityData> encodedEntity = VoxelCodec.Encode(in rawEntity, _brickRegistry, _paintRegistry);
            if (!encodedEntity.Success)
            {
                return new Result<StarterWreckPlacement>(
                    false,
                    default,
                    $"Failed to encode starter wreck \"{blueprintId}\": {encodedEntity.Message}",
                    encodedEntity.Exception);
            }

            return Result<StarterWreckPlacement>.FromSuccess(
                new StarterWreckPlacement(encodedEntity.Value, position, boundingRadius));
        }
        catch (Exception ex)
        {
            return new Result<StarterWreckPlacement>(
                false,
                default,
                $"Unexpected failure generating starter wreck \"{blueprintId}\": {ex.Message}",
                ex);
        }
    }

    /// <summary>Computes the tight bounding sphere radius around local origin covering every occupied voxel cell.</summary>
    internal static float ComputeBoundingRadius(VoxelObject voxelObject)
    {
        float maxDistSq = 0f;
        byte chunkSize = voxelObject.ChunkSize;
        int shift = BitOperations.TrailingZeroCount(chunkSize);
        int shift2 = shift * 2;
        int mask = chunkSize - 1;

        foreach (ChunkData chunk in voxelObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            int baseX = chunk.Coords.X * chunkSize;
            int baseY = chunk.Coords.Y * chunkSize;
            int baseZ = chunk.Coords.Z * chunkSize;
            Voxel[] voxels = chunk.Data.Voxels;

            for (var i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].ID == 0)
                {
                    continue;
                }

                float dx = MathF.Abs(baseX + (i & mask)) + 0.5f;
                float dy = MathF.Abs(baseY + ((i >> shift) & mask)) + 0.5f;
                float dz = MathF.Abs(baseZ + (i >> shift2)) + 0.5f;
                float distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > maxDistSq)
                {
                    maxDistSq = distSq;
                }
            }
        }

        return MathF.Sqrt(maxDistSq);
    }

    private static bool IntersectsClearanceZones(List<ClearanceZone> zones, Vector3 position, float radius)
    {
        for (var i = 0; i < zones.Count; i++)
        {
            ClearanceZone zone = zones[i];
            if (Intersection.SphereToSphere(zone.Position, zone.Radius, position, radius))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IntersectsAsteroids(List<AsteroidStructure> asteroids, Vector3 position, int radius)
    {
        for (var i = 0; i < asteroids.Count; i++)
        {
            AsteroidStructure asteroid = asteroids[i];
            if (Intersection.SphereToSphere(asteroid.Position, asteroid.Radius, position, radius))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Derives a stable, non-zero structure uuid from the seed and the structure's index, so
    ///     regenerating a level produces the same identities.
    /// </summary>
    private Uuid GenerateUuid(int index)
    {
        return Uuid.FromValue(((ulong)(uint)_foldedSeed << 32) | (uint)(index + 1));
    }

    /// <summary>
    ///     Derives a stable, non-zero uuid for the Adventure starter wreck that cannot collide with
    ///     sequential asteroid indices.
    /// </summary>
    internal Uuid GenerateStarterWreckUuid()
    {
        return Uuid.FromValue(((ulong)(uint)_foldedSeed << 32) | STARTER_WRECK_UUID_TAG);
    }

    private static int FoldSeed(long seed)
    {
        return (int)(seed ^ (seed >>> 32));
    }

    private readonly struct StarterWreckPlacement(in VoxelEntityData entity, Vector3 position, float boundingRadius)
    {
        public readonly VoxelEntityData Entity = entity;
        public readonly Vector3 Position = position;
        public readonly float BoundingRadius = boundingRadius;
    }
}
