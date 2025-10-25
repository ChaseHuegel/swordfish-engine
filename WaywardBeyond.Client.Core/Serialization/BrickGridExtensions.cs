using System.Collections.Generic;
using Swordfish.Bricks;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Saves;
using Int3 = (int X, int Y, int Z);

namespace WaywardBeyond.Client.Core.Serialization;

using ItemToProcess = (BrickGrid BrickGrid, Int3 Offset);

internal static class BrickGridExtensions
{
    public static RawBrick[] ToRawBricks(this BrickGrid value)
    {
        var rawBricks = new List<RawBrick>(value.Size);
        foreach (BrickGridItem item in value.GetBricks())
        {
            rawBricks.Add(new RawBrick((short)item.X, (short)item.Y, (short)item.Z, item.Brick.ID, item.Brick.Data, item.Brick.Orientation.ToByte()));
        }

        return rawBricks.ToArray();
    }
    
    public static IEnumerable<BrickGridItem> GetBricks(this BrickGrid value)
    {
        HashSet<BrickGrid> processed = [];
        var toProcess = new Stack<ItemToProcess>();
        toProcess.Push(new ItemToProcess(value, new Int3(0, 0, 0)));
        
        while (toProcess.TryPop(out ItemToProcess item))
        {
            BrickGrid brickGrid = item.BrickGrid;
            
            for (var z = 0; z < brickGrid.DimensionSize; z++)
            for (var y = 0; y < brickGrid.DimensionSize; y++)
            for (var x = 0; x < brickGrid.DimensionSize; x++)
            {
                Brick brick = brickGrid.Bricks[x, y, z];
                if (brick.ID == Brick.UNDEFINED_ID && brick.Data == 0)
                {
                    continue;
                }

                int gridX = x + item.Offset.X;
                int gridY = y + item.Offset.Y;
                int gridZ = z + item.Offset.Z;
                
                yield return new BrickGridItem(brick, gridX, gridY, gridZ);
            }

            for (var z = 0; z < brickGrid.NeighborGrids.GetLength(2); z++)
            for (var y = 0; y < brickGrid.NeighborGrids.GetLength(1); y++)
            for (var x = 0; x < brickGrid.NeighborGrids.GetLength(0); x++)
            {
                BrickGrid? neighborGrid = brickGrid.NeighborGrids[x, y, z];
                if (neighborGrid == null || !processed.Add(neighborGrid))
                {
                    continue;
                }

                var offset = new Int3(
                    (x - 1) * brickGrid.DimensionSize + item.Offset.X,
                    (y - 1) * brickGrid.DimensionSize + item.Offset.Y,
                    (z - 1) * brickGrid.DimensionSize + item.Offset.Z
                );
                toProcess.Push(new ItemToProcess(neighborGrid, offset));
            }
        }
    }
}