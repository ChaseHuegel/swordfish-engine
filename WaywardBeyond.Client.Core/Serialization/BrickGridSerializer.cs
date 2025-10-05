using System.Collections.Generic;
using Swordfish.Bricks;
using Swordfish.Library.Serialization;

namespace WaywardBeyond.Client.Core.Serialization;

internal class BrickGridSerializer : ISerializer<BrickGrid>
{
    public byte[] Serialize(BrickGrid value)
    {
        var rawBricks = new List<RawBrick>(value.Size);

        HashSet<BrickGrid> processed = [];
        var toProcess = new Stack<BrickGrid>();
        toProcess.Push(value);
        
        while (toProcess.TryPop(out BrickGrid? brickGrid))
        {
            for (var z = 0; z < brickGrid.NeighborGrids.GetLength(2); z++)
            for (var y = 0; y < brickGrid.NeighborGrids.GetLength(1); y++)
            for (var x = 0; x < brickGrid.NeighborGrids.GetLength(0); x++)
            {
                Brick brick = brickGrid.Bricks[x, y, z];
                if (brick.ID == Brick.UNDEFINED_ID)
                {
                    continue;
                }

                rawBricks.Add(new RawBrick(x, y, z, brick.ID, brick.Data));
            }

            for (var z = 0; z < brickGrid.NeighborGrids.GetLength(2); z++)
            for (var y = 0; y < brickGrid.NeighborGrids.GetLength(1); y++)
            for (var x = 0; x < brickGrid.NeighborGrids.GetLength(0); x++)
            {
                BrickGrid? neighborGrid = brickGrid.NeighborGrids[x, y, z];
                if (neighborGrid == null)
                {
                    continue;
                }

                if (processed.Add(neighborGrid))
                {
                    toProcess.Push(neighborGrid);
                }
            }
        }

        RawBrick[] rawBrickArr = rawBricks.ToArray();
        var rawBrickGrid = new RawBrickGrid(rawBrickArr);
        return rawBrickGrid.Serialize();
    }

    public BrickGrid Deserialize(byte[] data)
    {
        RawBrickGrid rawBrickGrid = RawBrickGrid.Deserialize(data);

        var brickGrid = new BrickGrid(dimensionSize: 16);
        for (var i = 0; i < rawBrickGrid.Bricks.Length; i++)
        {
            RawBrick rawBrick = rawBrickGrid.Bricks[i];
            brickGrid.Set(rawBrick.X, rawBrick.Y, rawBrick.Z, new Brick(rawBrick.ID, rawBrick.Data));
        }

        return brickGrid;
    }
}