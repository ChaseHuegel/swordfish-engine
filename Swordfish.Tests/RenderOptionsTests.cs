using Swordfish.Graphics;
using Xunit;

namespace Swordfish.Tests;

public class RenderOptionsTests
{
    [Fact]
    public void EqualOptionsHaveEqualHashCodes()
    {
        var left = new RenderOptions { AlphaMode = AlphaMode.Masked, AlphaCutoff = 0.25f, CastShadows = false };
        var right = new RenderOptions { AlphaMode = AlphaMode.Masked, AlphaCutoff = 0.25f, CastShadows = false };

        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void DifferentOptionsAreNotEqual()
    {
        var left = new RenderOptions();
        var right = new RenderOptions { DoubleFaced = true };

        Assert.False(left.Equals(right));
        Assert.False(left == right);
        Assert.True(left != right);
    }
}
