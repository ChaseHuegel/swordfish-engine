using System.Diagnostics;
using Swordfish.Bricks;
using Swordfish.ECS;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests
{
    public class BrickTests : TestBase
    {
        public BrickTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void GridCenterOfMass()
        {
            BrickGrid grid = new(16);

            grid.Set(0, 1, 0, new Brick(1));
            Output.WriteLine($"CenterOfMass: {grid.CenterOfMass}");

            grid.Set(0, 2, 0, new Brick(1));
            Output.WriteLine($"CenterOfMass: {grid.CenterOfMass}");

            grid.Set(2, 0, 0, new Brick(1));
            Output.WriteLine($"CenterOfMass: {grid.CenterOfMass}");
        }

        [Fact]
        public void SetToNeighbors()
        {
            BrickGrid grid = new(16);
            grid.Set(16, 0, 0, new Brick(1));
            grid.Set(-16, 0, 0, new Brick(1));
        }

        [Fact]
        public void SetToDistantNeighbors()
        {
            BrickGrid grid = new(16);
            grid.Set(32, 0, 0, new Brick(1));
            grid.Set(-32, 0, 0, new Brick(1));
            grid.Set(160, 0, 0, new Brick(1));
            grid.Set(-160, 0, 0, new Brick(1));
        }

        [Fact]
        public void BrickCountCascades()
        {
            BrickGrid grid = new(16);
            grid.Set(32, 0, 0, new Brick(1));
            grid.Set(-32, 0, 0, new Brick(1));
            grid.Set(160, 0, 0, new Brick(1));
            grid.Set(-160, 0, 0, new Brick(1));
            Assert.Equal(4, grid.Count);
        }
    }
}
