using System;
using System.Numerics;
using LibNoise;
using LibNoise.Primitive;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Generation.Noise;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Building;

namespace WaywardBeyond.Client.Core.Generation.Structures;

internal sealed class AsteroidGenerator(in int seed, in VoxelEntityBuilder voxelEntityBuilder, in IAssetDatabase<BrickInfo> brickDatabase)
{
    private readonly Randomizer _randomizer = new(seed);
    private readonly VoxelEntityBuilder _voxelEntityBuilder = voxelEntityBuilder;
    private readonly SimplexPerlin _simplexPerlin = new(seed, NoiseQuality.Fast);
    private readonly Voxel _rockVoxel = brickDatabase.Get("rock").Value.ToVoxel();
    private readonly Voxel _iceVoxel = brickDatabase.Get("ice").Value.ToVoxel();

    public void GenerateAt(Vector3 position, int diameter)
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
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
            
            voxelObject.Set(x - width, y - width, z - width, voxel);
        }
        
        float yaw = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        float pitch = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        float roll = _randomizer.NextFloat() * MathS.RADIANS_FULL_REVOLUTION;
        var orientation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
        
        _voxelEntityBuilder.Create(Guid.NewGuid(), voxelObject, position, orientation, Vector3.One);
    }
}