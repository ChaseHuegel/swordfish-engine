using System;
using System.Numerics;
using LibNoise;
using LibNoise.Primitive;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay.Generation.Noise;

namespace WaywardBeyond.Shared.Gameplay.Generation.Structures;

/// <summary>
///     Generates one rocky/icy asteroid as serialized voxel data. Correspondence to the former
///     client-side generator is exact: the same seeded randomizer, simplex noise and voxel indexing (via
///     <see cref="VoxelChunkWriter"/>), but it produces a <see cref="GeneratedVoxelEntity"/> instead of
///     building entities into a render-coupled store, so the server can run it headlessly and re-stream
///     the result.
/// </summary>
internal sealed class AsteroidGenerator(in int seed, in Voxel rockVoxel, in Voxel iceVoxel)
{
    private readonly Randomizer _randomizer = new(seed);
    private readonly SimplexPerlin _simplexPerlin = new(seed, NoiseQuality.Fast);
    private readonly Voxel _rockVoxel = rockVoxel;
    private readonly Voxel _iceVoxel = iceVoxel;

    public GeneratedVoxelEntity GenerateAt(Vector3 position, int diameter)
    {
        var writer = new VoxelChunkWriter(chunkSize: 16);
        Voxel voxel = _randomizer.NextFloat() > 0.5f ? _rockVoxel : _iceVoxel;

        int width = diameter / 2;
        int centerOfMass = diameter / 2;
        int radius = _randomizer.NextInt(width / 5, width);
        var origin = new Vector3(centerOfMass);
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

        return new GeneratedVoxelEntity(Uuid.NewUuid(), position, orientation, writer.GetChunkInfos());
    }
}