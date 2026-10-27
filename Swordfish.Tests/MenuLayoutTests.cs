using Reef.Constraints;
using Reef.UI;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class MenuLayoutTests
{
    [Fact]
    public void WideLayoutKeepsTwoColumns()
    {
        MenuLayout layout = MenuLayout.Compute(1920, 1080, maxPageWidth: 1160, twoColumn: true);

        Assert.False(layout.UseTabs);
        Assert.Equal(1160, layout.PageWidth);
        Assert.Equal(560, layout.PageHeight);
        Assert.Equal(0.4f, layout.LeftRatio);
    }

    [Fact]
    public void NarrowLayoutUsesTabsAndOnePane()
    {
        MenuLayout layout = MenuLayout.Compute(800, 720, maxPageWidth: 1160, twoColumn: true);

        Assert.True(layout.UseTabs);
        Assert.Equal(680, layout.PageWidth);
        Assert.True(layout.PageWidth >= MenuLayout.MIN_PAGE_WIDTH);
    }

    [Fact]
    public void PageFitsInsideTheWindow()
    {
        MenuLayout layout = MenuLayout.Compute(800, 600, maxPageWidth: 1160, twoColumn: false);

        Assert.True(layout.PageWidth <= 800);
        Assert.True(layout.PageHeight <= 600);
        Assert.True(layout.PageWidth >= MenuLayout.MIN_PAGE_WIDTH);
        Assert.True(layout.PageHeight >= MenuLayout.MIN_PAGE_HEIGHT);
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
    public void FooterButtonUsesTheSharedSlot()
    {
        Widgets.ActionButtonOptions options = Widgets.ActionButtonOptions
            .Menu(ButtonVariant.Secondary, soundEffectService: null)
            .FooterButton(MenuLayout.PRIMARY_BUTTON_WIDTH);

        Constraints? layout = options.Layout;

        Assert.NotNull(layout);
        Assert.Equal(MenuLayout.PRIMARY_BUTTON_WIDTH, ((Fixed)layout.Value.Width!).Calculate(0));
        Assert.Equal(MenuLayout.FOOTER_BUTTON_HEIGHT, ((Fixed)layout.Value.Height!).Calculate(0));
        Assert.Equal(Anchors.Center, layout.Value.Anchors);
    }
}
