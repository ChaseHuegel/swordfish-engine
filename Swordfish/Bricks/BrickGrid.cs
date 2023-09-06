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

    public Brick Get(int x, int y, int z)
    {
        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
        {
            if (TryGetNeighbor(x, y, z, out BrickGrid neighbor))
            {
                if (x >= DimensionSize)
                    x += x < 0 ? DimensionSize : -DimensionSize;

                if (y >= DimensionSize)
                    y += y < 0 ? DimensionSize : -DimensionSize;

                if (z >= DimensionSize)
                    z += z < 0 ? DimensionSize : -DimensionSize;

                return neighbor.Get(x, y, z);
            }
        }

        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
            return default;

        lock (LockObject)
        {
            return Bricks[x, y, z];
        }
    }

    public bool Set(int x, int y, int z, Brick brick)
    {
        int previousCount = Count;

        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
        {
            if (TryGetOrAddNeighbor(x, y, z, out BrickGrid neighbor))
            {
                Vector3 newPoint = new Vector3(x, y, z);
                if (x >= DimensionSize)
                    x += x < 0 ? DimensionSize : -DimensionSize;

                if (y >= DimensionSize)
                    y += y < 0 ? DimensionSize : -DimensionSize;

                if (z >= DimensionSize)
                    z += z < 0 ? DimensionSize : -DimensionSize;

                int neighborOldCount = neighbor.Count;
                bool success = neighbor.Set(x, y, z, brick);

                NeighorBrickCount += neighbor.Count - neighborOldCount;

                UpdateCenterOfMass(previousCount, newPoint);
                return success;
            }
        }

        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
            return false;

        lock (LockObject)
        {
            Brick currentBrick = Bricks[x, y, z];
            if (currentBrick != brick)
            {
                int newBrickCount = BrickCount + (brick == Brick.EMPTY ? -1 : 1);

                Bricks[x, y, z] = brick;
                BrickCount = newBrickCount;

                UpdateCenterOfMass(previousCount, new Vector3(x, y, z));
            }
        }

        Dirty = true;
        return true;
    }

    private void UpdateCenterOfMass(int previousCount, Vector3 newPoint)
    {
        CenterOfMass = ((CenterOfMass * previousCount) + newPoint) / Count;
    }

    public bool Build()
    {
        if (Building || !Dirty)
            return false;

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
        return true;
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
            {
                Vector3 targetThis = new(
                    Math.Abs(targetNeighbor.X - 2),
                    Math.Abs(targetNeighbor.Y - 2),
                    Math.Abs(targetNeighbor.Z - 2)
                );
                neighbor = new BrickGrid(DimensionSize);
                neighbor.NeighborGrids[(int)targetThis.X, (int)targetThis.Y, (int)targetThis.Z] = this;
                NeighborGrids[(int)targetNeighbor.X, (int)targetNeighbor.Y, (int)targetNeighbor.Z] = neighbor;
            }

            return true;
        }

        neighbor = this;
        return false;
    }

    private bool TryGetNeighbor(int x, int y, int z, out BrickGrid neighbor)
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
                return false;

            return true;
        }

        neighbor = this;
        return false;
    }
}
