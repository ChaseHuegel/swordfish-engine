using System.Numerics;

namespace Swordfish.Bricks;

public class BrickGrid
{
    public readonly int DimensionSize;

    public Vector3 CenterOfMass { get; private set; }

    // ReSharper disable once UnusedMember.Global
    public int Size { get; private set; }

    public int Count => _brickCount + _neighborBrickCount;

    public readonly Brick[,,] Bricks;
    public readonly BrickGrid?[,,] NeighborGrids = new BrickGrid[3, 3, 3];
    public readonly List<BrickGrid> Subgrids = [];
    private readonly object _lockObject = new();

    private volatile int _neighborBrickCount;
    private volatile int _brickCount;
    private volatile bool _building;
    private volatile bool _dirty;

    public BrickGrid(int dimensionSize)
    {
        if (dimensionSize % 4 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensionSize), dimensionSize, "Value must be divisible by 4.");
        }

        DimensionSize = dimensionSize;
        Bricks = new Brick[dimensionSize, dimensionSize, dimensionSize];
    }

    public Brick Get(int x, int y, int z)
    {
        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
        {
            if (TryGetNeighbor(x, y, z, out BrickGrid? neighbor))
            {
                if (x >= DimensionSize || x < 0)
                {
                    x += x < 0 ? DimensionSize : -DimensionSize;
                }

                if (y >= DimensionSize || y < 0)
                {
                    y += y < 0 ? DimensionSize : -DimensionSize;
                }

                if (z >= DimensionSize || z < 0)
                {
                    z += z < 0 ? DimensionSize : -DimensionSize;
                }

                return neighbor.Get(x, y, z);
            }
        }

        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
        {
            return default;
        }

        lock (_lockObject)
        {
            return Bricks[x, y, z];
        }
    }

    public bool Set(int x, int y, int z, Brick brick)
    {
        int previousCount = Count;

        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
        {
            if (TryGetOrAddNeighbor(x, y, z, out BrickGrid? neighbor))
            {
                var newPoint = new Vector3(x, y, z);
                if (x >= DimensionSize || x < 0)
                {
                    x += x < 0 ? DimensionSize : -DimensionSize;
                }

                if (y >= DimensionSize || y < 0)
                {
                    y += y < 0 ? DimensionSize : -DimensionSize;
                }

                if (z >= DimensionSize || z < 0)
                {
                    z += z < 0 ? DimensionSize : -DimensionSize;
                }

                int neighborOldCount = neighbor.Count;
                bool success = neighbor.Set(x, y, z, brick);

                Interlocked.Exchange(ref _neighborBrickCount, _neighborBrickCount + neighbor.Count - neighborOldCount);

                UpdateCenterOfMass(previousCount, newPoint);
                return success;
            }
        }

        if (x >= DimensionSize || y >= DimensionSize || z >= DimensionSize || x < 0 || y < 0 || z < 0)
        {
            return false;
        }

        lock (_lockObject)
        {
            Brick currentBrick = Bricks[x, y, z];
            if (currentBrick != brick)
            {
                int newBrickCount = _brickCount + (brick == Brick.Empty ? -1 : 1);

                Bricks[x, y, z] = brick;
                _brickCount = newBrickCount;

                UpdateCenterOfMass(previousCount, new Vector3(x, y, z));
            }
        }

        _dirty = true;
        return true;
    }

    private void UpdateCenterOfMass(int previousCount, Vector3 newPoint)
    {
        CenterOfMass = ((CenterOfMass * previousCount) + newPoint) / Count;
    }

    //  TODO Is this useful anymore? Implement or remove.
    // ReSharper disable once UnusedMember.Global
    public bool Build()
    {
        if (_building || !_dirty)
        {
            return false;
        }

        _dirty = false;
        _building = true;
        lock (_lockObject)
        {
            for (var x = 0; x < DimensionSize; x++)
            {
                for (var y = 0; y < DimensionSize; y++)
                {
                    for (var z = 0; z < DimensionSize; z++)
                    {
                        Bricks[x, y, z].Build();
                    }
                }
            }

            for (var i = 0; i < Subgrids.Count; i++)
            {
                Subgrids[i].Build();
            }

            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    for (var z = 0; z < 3; z++)
                    {
                        BrickGrid? neighborGrid = NeighborGrids[x, y, z];
                        if (neighborGrid != null && !neighborGrid._building)
                        {
                            neighborGrid.Build();
                        }
                    }
                }
            }
        }

        _building = false;
        return true;
    }

    private bool TryGetOrAddNeighbor(int x, int y, int z, out BrickGrid? neighbor)
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
            if (neighbor != null)
            {
                return true;
            }

            Vector3 targetThis = new(
                Math.Abs(targetNeighbor.X - 2),
                Math.Abs(targetNeighbor.Y - 2),
                Math.Abs(targetNeighbor.Z - 2)
            );
            neighbor = new BrickGrid(DimensionSize);
            neighbor.NeighborGrids[(int)targetThis.X, (int)targetThis.Y, (int)targetThis.Z] = this;
            NeighborGrids[(int)targetNeighbor.X, (int)targetNeighbor.Y, (int)targetNeighbor.Z] = neighbor;
            return true;
        }

        neighbor = this;
        return false;
    }

    private bool TryGetNeighbor(int x, int y, int z, out BrickGrid? neighbor)
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
            return neighbor != null;
        }

        neighbor = this;
        return false;
    }
}
