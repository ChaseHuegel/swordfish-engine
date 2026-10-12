using System.Numerics;
using LibNoise;
using LibNoise.Primitive;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay.Saves;

namespace WaywardBeyond.Server.Generation;

/// <summary>A type for generating voxel asteroid structures.</summary>
internal sealed class AsteroidGenerator(IBrickRegistry brickRegistry, int seed, Voxel rockVoxel, Voxel iceVoxel)
{
    private readonly Randomizer _randomizer = new(seed);
    private readonly SimplexPerlin _simplexPerlin = new(seed, NoiseQuality.Fast);

    /// <summary>Generate an asteroid using the provided parameters.</summary>
    public VoxelEntityData GenerateAt(Vector3 position, int diameter, Uuid uuid)
    {
        var writer = new VoxelChunkWriter(chunkSize: 16);
        Voxel voxel = _randomizer.NextFloat() > 0.5f ? rockVoxel : iceVoxel;

        int width = diameter / 2;
        int radius = _randomizer.NextInt(width / 5, width);
        var origin = new Vector3(width);
        int offset = _randomizer.NextInt(1000);
        float frequency = _randomizer.NextFloat() * 0.03f + 0.02f;
        float amplitude = _randomizer.NextFloat() * 0.5f + 0.2f;

        for (var x = 0; x < diameter; x++)
        for (var y = 0; y < diameter; y++)
        for (var z = 0; z < diameter; z++)
        {
            var pos = new Vector3(x, y, z);
            float distance = Vector3.Distance(pos, origin);

            float percentDistance = distance / radius;

            float value = _simplexPerlin.GetLayeredNoiseValue(2, frequency, amplitude, x + offset * diameter, y, z);

            bool solid = percentDistance < value;
            if (!solid)
            {
                continue;
            }

            writer.Set(x - width, y - width, z - width, voxel);
        }

        float yaw = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        float pitch = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        float roll = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        var orientation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
        ChunkInfo[] chunkInfos = writer.GetChunkInfos();
        string[]? brickPalette = VoxelEntityDataCodec.EncodePalette(chunkInfos, brickRegistry);

        return new VoxelEntityData(
            uuid.ToValue(),
            position.X, position.Y, position.Z,
            orientation.X, orientation.Y, orientation.Z, orientation.W,
            _ScaleX: 1, _ScaleY: 1, _ScaleZ: 1,
            chunkInfos,
            brickPalette
        );
    }
}
