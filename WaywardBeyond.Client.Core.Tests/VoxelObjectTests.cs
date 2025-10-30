using System.Diagnostics;
using WaywardBeyond.Client.Core.Voxels;

namespace WaywardBeyond.Client.Core.Tests;

public class VoxelObjectTests
{
    [Test]
    public void Test1()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        
        voxelObject.Set(-1, 0, 0, new Voxel(1, 0, 0));
        voxelObject.Set(0, 0, 0, new Voxel(2, 0, 0));
        voxelObject.Set(8, 8, 8, new Voxel(3, 0, 0));
        voxelObject.Set(15, 15, 15, new Voxel(4, 0, 0));
        voxelObject.Set(16, 16, 16, new Voxel(5, 0, 0));
        voxelObject.Set(24, 24, 24, new Voxel(6, 0, 0));
        voxelObject.Set(31, 31, 31, new Voxel(7, 0, 0));

        foreach (Voxel voxel in voxelObject)
        {
            if (voxel.ID == 0)
            {
                continue;
            }
            Console.WriteLine("Found voxel: " + voxel.ID);
        }
        
        foreach (ref Voxel voxel in voxelObject)
        {
            if (voxel.ID == 0)
            {
                continue;
            }
            voxel.ID++;
        }
        
        Console.WriteLine();
        
        foreach (Voxel voxel in voxelObject)
        {
            if (voxel.ID == 0)
            {
                continue;
            }
            Console.WriteLine("Found voxel: " + voxel.ID);
        }
        
        foreach (ref Voxel voxel in voxelObject)
        {
            if (voxel.ID == 0)
            {
                continue;
            }
            voxel.ID--;
        }
        
        Console.WriteLine();

        var sw = Stopwatch.StartNew();
        var samples = 0;
        foreach (VoxelSample sample in voxelObject.GetSampler())
        {
            samples++;
            if (!sample.HasAny())
            {
                continue;
            }

            Console.WriteLine($"Sample: Chunk: {sample.ChunkCoords.X}, {sample.ChunkCoords.Y}, {sample.ChunkCoords.Z}, Coords: {sample.Coords.X}, {sample.Coords.Y}, {sample.Coords.Z}, Center: {sample.Center.ID}, Above: {sample.Above.ID}, Below: {sample.Below.ID}, Left: {sample.Left.ID}, Right: {sample.Right.ID}, Ahead: {sample.Ahead.ID}, Behind: {sample.Behind.ID}");
        }
        sw.Stop();
        Console.WriteLine($"{samples} samples in {sw.ElapsedMilliseconds} ms");
    }
}