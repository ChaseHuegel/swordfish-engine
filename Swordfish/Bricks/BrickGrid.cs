using System.Numerics;

namespace Swordfish.Bricks;

public class BrickGrid
{
    public readonly int DimensionSize;

    public Vector3 CenterOfMass { get; private set; }

    public int Size { get; private set; }

    public int Count => BrickCount + NeighorBrickCount;

    public readonly Brick[,,] Bricks;
    public readonly BrickGrid[,,] NeighborGrids = new BrickGrid[3, 3, 3];
    public readonly List<BrickGrid> Subgrids = new();
    private readonly object LockObject = new();

    private volatile int NeighorBrickCount;
    private volatile int BrickCount;
    private volatile bool Building;
    private volatile bool Dirty;

    public BrickGrid(int dimensionSize)
    {
        if (dimensionSize % 4 != 0)
            throw new ArgumentOutOfRangeException(nameof(dimensionSize), dimensionSize, "Value must be divisible by 4.");

        DimensionSize = dimensionSize;
        Bricks = new Brick[dimensionSize, dimensionSize, dimensionSize];
    }

    public bool Set(int x, int y, int z, Brick brick)
    {
        if (TryGetOrAddNeighbor(x, y, z, out BrickGrid neighbor))
        {
            if (x >= DimensionSize)
                x += x < 0 ? DimensionSize : -DimensionSize;

            if (y >= DimensionSize)
                y += y < 0 ? DimensionSize : -DimensionSize;

            if (z >= DimensionSize)
                z += z < 0 ? DimensionSize : -DimensionSize;

            int neighborOldCount = neighbor.Count;
            bool success = neighbor.Set(x, y, z, brick);

            NeighorBrickCount += neighbor.Count - neighborOldCount;
            //  TODO cascade CenterOfMass?

            return success;
        }

        lock (LockObject)
        {
            Brick currentBrick = Bricks[x, y, z];
            if (currentBrick != brick)
            {
                int newBrickCount = BrickCount + (brick == Brick.EMPTY ? -1 : 1);
                CenterOfMass = ((CenterOfMass * BrickCount) + new Vector3(x, y, z)) / newBrickCount;

                Bricks[x, y, z] = brick;
                BrickCount = newBrickCount;
            }
        }

        Dirty = true;
        return true;
    }

    private void Build()
    {
        if (Building || !Dirty)
            return;

        Dirty = false;
        Building = true;
        lock (LockObject)
        {
            for (int x = 0; x < DimensionSize; x++)
            {
                for (int y = 0; y < DimensionSize; y++)
                {
                    for (int z = 0; z < DimensionSize; z++)
                    {
                        Bricks[x, y, z].Build();
                    }
                }
            }

            for (int i = 0; i < Subgrids.Count; i++)
            {
                Subgrids[i].Build();
            }

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int z = 0; z < 3; z++)
                    {
                        BrickGrid neighorGrid = NeighborGrids[x, y, z];
                        if (neighorGrid != null && !neighorGrid.Building)
                            neighorGrid.Build();
                    }
                }
            }
        }

        Building = false;
    }

    private bool TryGetOrAddNeighbor(int x, int y, int z, out BrickGrid neighbor)
    {
        int xOffset = x >> 4;
        int yOffset = y >> 4;
        int zOffset = z >> 4;

        Vector3 targetNeighbor = new(
            xOffset != 0 ? (xOffset < 0 ? 0 : 2) : 1,
            yOffset != 0 ? (yOffset < 0 ? 0 : 2) : 1,
            zOffset != 0 ? (zOffset < 0 ? 0 : 2) : 1
        );

        if (targetNeighbor != Vector3.One)
        {
            neighbor = NeighborGrids[(int)targetNeighbor.X, (int)targetNeighbor.Y, (int)targetNeighbor.Z];

            if (neighbor == null)
                NeighborGrids[(int)targetNeighbor.X, (int)targetNeighbor.Y, (int)targetNeighbor.Z] = neighbor = new BrickGrid(DimensionSize);

            return true;
        }

        neighbor = this;
        return false;
    }
}
