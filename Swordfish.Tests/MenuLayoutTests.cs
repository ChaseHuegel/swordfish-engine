using WaywardBeyond.Client.UI.Layers.Menus;
using Xunit;

namespace Swordfish.Tests;

public class MenuLayoutTests
{
    [Fact]
    public void WideLayoutSplitsIntoTwoPanes()
    {
        MenuLayout layout = MenuLayout.Compute(1920, 1080, maxPageWidth: 1160, twoColumn: true);

        Assert.Equal(12, layout.Left.Top);
        Assert.Equal(12, layout.Right.Top);
        Assert.False(layout.UseTabs);
        Assert.True(layout.Left.Size.X > 0);
        Assert.True(layout.Right.Size.X > 0);
        Assert.True(layout.Left.Right < layout.Right.Left);
        Assert.Equal(layout.PageWidth, layout.Right.Right);
    }

    [Fact]
    public void NarrowLayoutUsesTabsAndOnePane()
    {
        MenuLayout layout = MenuLayout.Compute(800, 720, maxPageWidth: 1160, twoColumn: true);

        Assert.True(layout.UseTabs);
        Assert.Equal(0, layout.Left.Size.X);
        Assert.True(layout.Tabs.Bottom <= layout.Right.Top);
        Assert.Equal(layout.PageWidth, layout.Right.Size.X);
    }

    [Fact]
    public void CreditsCanUseTallerPage()
    {
        MenuLayout layout = MenuLayout.Compute(
            1920,
            1080,
            maxPageWidth: 1160,
            twoColumn: false,
            maxPageHeight: 720
        );

        Assert.Equal(720, layout.PageHeight);
    }

    [Fact]
    public void FooterButtonsStayInsideThePage()
    {
        MenuLayout layout = MenuLayout.Compute(1280, 720, maxPageWidth: 960, twoColumn: false);

        Assert.True(layout.ReturnButton.Left >= 0);
        Assert.True(layout.ReturnButton.Right <= layout.PageWidth);
        Assert.True(layout.PrimaryButton.Left >= 0);
        Assert.True(layout.PrimaryButton.Right <= layout.PageWidth);
        Assert.Equal(layout.PageHeight, layout.Footer.Bottom);
    }
}
