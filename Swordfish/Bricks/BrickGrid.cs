namespace Swordfish.Bricks;

public class BrickGrid
{
    public int DimensionSize { get; private set; }

    private readonly Brick[,,] Bricks;
    private readonly BrickGrid[,,] NeighborGrids = new BrickGrid[3, 3, 3];
    private readonly List<BrickGrid> Subgrids = new();

    private bool Building;

    public BrickGrid(int dimensionSize)
    {
        DimensionSize = dimensionSize;
        Bricks = new Brick[dimensionSize, dimensionSize, dimensionSize];
    }

    public void Build()
    {
        if (Building)
            return;

        Building = true;

        for (int x = 0; x < DimensionSize; x++)
        {
            for (int y = 0; y < DimensionSize; y++)
            {
                for (int z = 0; z < DimensionSize; z++)
                {
                    Brick brick = Bricks[x, y, z];
                    BuildBrick(brick);
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

        Building = false;
    }

    private void BuildBrick(Brick brick)
    {
    }
}
