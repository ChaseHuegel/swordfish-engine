using System.Collections.Generic;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Tracks 2D projection min/max depth bounds across XY, XZ, and ZY axes.
/// </summary>
public sealed class DepthState
{
    public readonly Dictionary<Int2, Int2> XY = [];
    public readonly Dictionary<Int2, Int2> XZ = [];
    public readonly Dictionary<Int2, Int2> ZY = [];

    /// <summary>Updates depth bounds when a solid culler voxel is added.</summary>
    public void Add(int x, int y, int z)
    {
        var keyXY = new Int2(x, y);
        if (!XY.TryGetValue(keyXY, out Int2 depthZ))
        {
            XY[keyXY] = new Int2(z, z);
        }
        else
        {
            if (z < depthZ.Min) depthZ.Min = z;
            if (z > depthZ.Max) depthZ.Max = z;
            XY[keyXY] = depthZ;
        }

        var keyXZ = new Int2(x, z);
        if (!XZ.TryGetValue(keyXZ, out Int2 depthY))
        {
            XZ[keyXZ] = new Int2(y, y);
        }
        else
        {
            if (y < depthY.Min) depthY.Min = y;
            if (y > depthY.Max) depthY.Max = y;
            XZ[keyXZ] = depthY;
        }

        var keyZY = new Int2(z, y);
        if (!ZY.TryGetValue(keyZY, out Int2 depthX))
        {
            ZY[keyZY] = new Int2(x, x);
        }
        else
        {
            if (x < depthX.Min) depthX.Min = x;
            if (x > depthX.Max) depthX.Max = x;
            ZY[keyZY] = depthX;
        }
    }

    /// <summary>Updates depth bounds when a solid culler voxel is removed.</summary>
    public void Remove(VoxelObject voxelObject, IBrickDatabase brickDatabase, int x, int y, int z)
    {
        var keyXY = new Int2(x, y);
        if (XY.TryGetValue(keyXY, out Int2 depthZ))
        {
            if (z == depthZ.Min && z == depthZ.Max)
            {
                XY.Remove(keyXY);
            }
            else if (z == depthZ.Min)
            {
                var found = false;
                for (int cz = z + 1; cz <= depthZ.Max; cz++)
                {
                    Voxel v = voxelObject.Get(x, y, cz);
                    if (v.ID == 0 || !brickDatabase.IsCuller(v, v.GetShapeLight().Shape))
                    {
                        continue;
                    }
                    
                    depthZ.Min = cz;
                    XY[keyXY] = depthZ;
                    found = true;
                    break;
                }

                if (!found)
                {
                    XY.Remove(keyXY);
                }
            }
            else if (z == depthZ.Max)
            {
                var found = false;
                for (int cz = z - 1; cz >= depthZ.Min; cz--)
                {
                    Voxel v = voxelObject.Get(x, y, cz);
                    if (v.ID == 0 || !brickDatabase.IsCuller(v, v.GetShapeLight().Shape))
                    {
                        continue;
                    }
                    
                    depthZ.Max = cz;
                    XY[keyXY] = depthZ;
                    found = true;
                    break;
                }

                if (!found)
                {
                    XY.Remove(keyXY);
                }
            }
        }

        var keyXZ = new Int2(x, z);
        if (XZ.TryGetValue(keyXZ, out Int2 depthY))
        {
            if (y == depthY.Min && y == depthY.Max)
            {
                XZ.Remove(keyXZ);
            }
            else if (y == depthY.Min)
            {
                var found = false;
                for (int cy = y + 1; cy <= depthY.Max; cy++)
                {
                    Voxel v = voxelObject.Get(x, cy, z);
                    if (v.ID == 0 || !brickDatabase.IsCuller(v, v.GetShapeLight().Shape))
                    {
                        continue;
                    }
                    
                    depthY.Min = cy;
                    XZ[keyXZ] = depthY;
                    found = true;
                    break;
                }

                if (!found)
                {
                    XZ.Remove(keyXZ);
                }
            }
            else if (y == depthY.Max)
            {
                var found = false;
                for (int cy = y - 1; cy >= depthY.Min; cy--)
                {
                    Voxel v = voxelObject.Get(x, cy, z);
                    if (v.ID == 0 || !brickDatabase.IsCuller(v, v.GetShapeLight().Shape))
                    {
                        continue;
                    }
                    
                    depthY.Max = cy;
                    XZ[keyXZ] = depthY;
                    found = true;
                    break;
                }

                if (!found)
                {
                    XZ.Remove(keyXZ);
                }
            }
        }

        var keyZY = new Int2(z, y);
        if (ZY.TryGetValue(keyZY, out Int2 depthX))
        {
            if (x == depthX.Min && x == depthX.Max)
            {
                ZY.Remove(keyZY);
            }
            else if (x == depthX.Min)
            {
                var found = false;
                for (int cx = x + 1; cx <= depthX.Max; cx++)
                {
                    Voxel v = voxelObject.Get(cx, y, z);
                    if (v.ID == 0 || !brickDatabase.IsCuller(v, v.GetShapeLight().Shape))
                    {
                        continue;
                    }
                    
                    depthX.Min = cx;
                    ZY[keyZY] = depthX;
                    found = true;
                    break;
                }

                if (!found)
                {
                    ZY.Remove(keyZY);
                }
            }
            else if (x == depthX.Max)
            {
                var found = false;
                for (int cx = x - 1; cx >= depthX.Min; cx--)
                {
                    Voxel v = voxelObject.Get(cx, y, z);
                    if (v.ID == 0 || !brickDatabase.IsCuller(v, v.GetShapeLight().Shape))
                    {
                        continue;
                    }
                    
                    depthX.Max = cx;
                    ZY[keyZY] = depthX;
                    found = true;
                    break;
                }

                if (!found)
                {
                    ZY.Remove(keyZY);
                }
            }
        }
    }

    /// <summary>Returns true if the coordinates represent an exterior ambient light source.</summary>
    public bool IsAmbientSource(int x, int y, int z)
    {
        //  A column with no solid cells is open along Y. The cell sees space above
        //  or below, so ambient light reaches it without decay.
        bool hasY = XZ.TryGetValue(new Int2(x, z), out Int2 depthY);
        bool openY = !hasY || y < depthY.Min || y > depthY.Max;
        if (openY)
        {
            return true;
        }

        bool hasZ = XY.TryGetValue(new Int2(x, y), out Int2 depthZ);
        bool openZ = !hasZ || z < depthZ.Min || z > depthZ.Max;
        bool hasX = ZY.TryGetValue(new Int2(z, y), out Int2 depthX);
        bool openX = !hasX || x < depthX.Min || x > depthX.Max;

        bool enclosedY = y > depthY.Min && y < depthY.Max;
        bool enclosedZ = hasZ && z > depthZ.Min && z < depthZ.Max;
        bool enclosedX = hasX && x > depthX.Min && x < depthX.Max;

        //  A horizontal opening only seeds the cell when the other two axes do
        //  not enclose it. This keeps tunnel and cavity light decaying inward.
        if (openZ && (!enclosedX || !enclosedY))
        {
            return true;
        }

        if (openX && (!enclosedY || !enclosedZ))
        {
            return true;
        }

        return false;
    }

    /// <summary>Populates depth bounds from all resident chunks in a voxel object.</summary>
    public void Populate(VoxelObject voxelObject, IBrickDatabase brickDatabase)
    {
        XY.Clear();
        XZ.Clear();
        ZY.Clear();

        foreach (ChunkData chunk in voxelObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            foreach (VoxelSample sample in chunk.GetSampler())
            {
                if (sample.Center.ID == 0 || !brickDatabase.IsCuller(sample.Center, sample.Center.GetShapeLight().Shape))
                {
                    continue;
                }

                int x = sample.Coords.X + sample.ChunkOffset.X;
                int y = sample.Coords.Y + sample.ChunkOffset.Y;
                int z = sample.Coords.Z + sample.ChunkOffset.Z;

                Add(x, y, z);
            }
        }
    }
}
