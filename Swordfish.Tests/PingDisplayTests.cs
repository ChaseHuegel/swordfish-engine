using System.Numerics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class PingDisplayTests
{
    [Fact]
    public void UnknownPingUsesDimSignalAndDash()
    {
        Widgets.PingDisplay display = Widgets.ResolvePing(null);

        Assert.Equal(MenuGlyphs.Signal, display.Glyph);
        Assert.Equal("-", display.Text);
        Assert.Equal(UiTheme.TextDim, display.Color);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(59)]
    [InlineData(60)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(149)]
    [InlineData(150)]
    [InlineData(249)]
    public void PingBandsUseExpectedColors(int ping)
    {
        Widgets.PingDisplay display = Widgets.ResolvePing(ping);

        Vector4 expected = ping switch
        {
            < 60 => UiTheme.Success,
            < 100 => UiTheme.Warning,
            < 150 => UiTheme.Primary,
            _ => UiTheme.Danger,
        };

        Assert.Equal(MenuGlyphs.Signal, display.Glyph);
        Assert.Equal(expected, display.Color);
        Assert.Equal($"{ping} ms", display.Text);
    }

    [Fact]
    public void UnreachablePingUsesXMark()
    {
        Widgets.PingDisplay display = Widgets.ResolvePing(250);

        Assert.Equal(MenuGlyphs.XMark, display.Glyph);
        Assert.Equal("-", display.Text);
        Assert.Equal(UiTheme.Danger, display.Color);
    }
}
