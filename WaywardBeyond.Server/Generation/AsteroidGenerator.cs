using System;
using System.Numerics;
using LibNoise;
using LibNoise.Primitive;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Generation;

/// <summary>A type for generating voxel asteroid structures.</summary>
internal sealed class AsteroidGenerator(IBrickRegistry brickRegistry, int seed, AsteroidConfig config)
{
    private readonly AsteroidConfig _config = config;
    private readonly Randomizer _randomizer = new(seed);
    private readonly SimplexPerlin _simplexPerlin = new(seed, NoiseQuality.Fast);
    private readonly Voxel[] _materialVoxels = ResolveMaterials(brickRegistry, config);

    /// <summary>Generate an asteroid using the provided parameters.</summary>
    public VoxelEntityData GenerateAt(Vector3 position, int diameter, Uuid uuid)
    {
        var writer = new VoxelChunkWriter(chunkSize: 16);
        Voxel voxel = _materialVoxels[_randomizer.NextInt(_materialVoxels.Length)];

        int width = diameter / 2;
        int radius = _randomizer.NextInt(Math.Max(1, width / _config.RadiusMinDivisor), width);
        var origin = new Vector3(width);
        int offset = _randomizer.NextInt(_config.NoiseOffsetRange);
        float frequency = _randomizer.NextFloat() * _config.NoiseFrequencyRange + _config.NoiseFrequencyMin;
        float amplitude = _randomizer.NextFloat() * _config.NoiseAmplitudeRange + _config.NoiseAmplitudeMin;

        for (var x = 0; x < diameter; x++)
        {
            for (var y = 0; y < diameter; y++)
            {
                for (var z = 0; z < diameter; z++)
                {
                    var pos = new Vector3(x, y, z);
                    float distance = Vector3.Distance(pos, origin);

                    float percentDistance = distance / radius;

                    float value = _simplexPerlin.GetLayeredNoiseValue(_config.NoiseOctaves, frequency, amplitude, x + offset * diameter, y, z);

                    bool solid = percentDistance < value;
                    if (!solid)
                    {
                        continue;
                    }

                    writer.Set(x - width, y - width, z - width, voxel);
                }
            }
        }

        float yaw = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        float pitch = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        float roll = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        var orientation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
        ChunkInfo[] chunkInfos = writer.GetChunkInfos();
        
        var voxelEntityData = new VoxelEntityData(
            uuid.ToValue(),
            position.X, position.Y, position.Z,
            orientation.X, orientation.Y, orientation.Z, orientation.W,
            _ScaleX: 1, _ScaleY: 1, _ScaleZ: 1,
            chunkInfos,
            _BrickPalette: null
        );
        
        return VoxelCodec.Encode(voxelEntityData, brickRegistry);
    }

    private static Voxel[] ResolveMaterials(IBrickRegistry brickRegistry, in AsteroidConfig config)
    {
        string[] names = config.Materials.Get();
        if (names.Length == 0)
        {
            throw new InvalidOperationException("Level generation requires at least one asteroid material.");
        }

        var voxels = new Voxel[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            ushort id = brickRegistry.Id(names[i]);
            if (id == 0)
            {
                throw new InvalidOperationException(
                    $"Level generation material \"{names[i]}\" is not a known brick; an unknown material would produce air.");
            }

            voxels[i] = new Voxel(id, _ShapeLight: 0, _Orientation: 0);
        }

        return voxels;
    }
}