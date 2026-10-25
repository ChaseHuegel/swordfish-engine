using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class UiThemeResolverTests
{
    [Fact]
    public void PrimaryButtonUsesAccentCapsAndBrightText()
    {
        ControlStyle style = UiThemeResolver.ResolveButton(ButtonVariant.Primary, ControlState.Normal);

        Assert.Equal(UiTheme.Primary, style.Border);
        Assert.Equal(UiTheme.PrimaryBright, style.Text);
        Assert.Equal(UiMetrics.PanelBorderThickness, style.BorderThickness);
        Assert.Equal(UiMetrics.SelectionCapLength, style.Caps.Length);
    }

    [Fact]
    public void PrimaryButtonBrightensOnHover()
    {
        ControlStyle style = UiThemeResolver.ResolveButton(ButtonVariant.Primary, ControlState.Hover);

        Assert.Equal(UiTheme.PrimaryBright, style.Border);
        Assert.Equal(UiTheme.Text, style.Text);
        Assert.Equal(UiTheme.ButtonHover, style.Fill);
    }

    [Fact]
    public void SecondaryButtonUsesAccentTextWhenPressed()
    {
        ControlStyle style = UiThemeResolver.ResolveButton(ButtonVariant.Secondary, ControlState.Pressed);

        Assert.Equal(UiTheme.AccentDark, style.Fill);
        Assert.Equal(UiTheme.AccentBright, style.Text);
    }

    [Fact]
    public void SubtleButtonHasNoBorder()
    {
        ControlStyle style = UiThemeResolver.ResolveButton(ButtonVariant.Subtle, ControlState.Normal);

        Assert.Equal(UiTheme.Transparent, style.Fill);
        Assert.Equal(0, style.BorderThickness);
        Assert.Equal(UiTheme.TextMuted, style.Text);
    }

    [Fact]
    public void DangerButtonUsesDangerBorder()
    {
        ControlStyle style = UiThemeResolver.ResolveButton(ButtonVariant.Danger, ControlState.Normal);

        Assert.Equal(UiTheme.Danger, style.Border);
        Assert.Equal(UiTheme.DangerBright, style.Text);
    }

    [Fact]
    public void DisabledButtonUsesDimText()
    {
        ControlStyle style = UiThemeResolver.ResolveButton(ButtonVariant.Primary, ControlState.Disabled);

        Assert.Equal(UiTheme.PanelInner, style.Fill);
        Assert.Equal(UiTheme.BorderInner, style.Border);
        Assert.Equal(UiTheme.TextDim, style.Text);
        Assert.Equal(0, style.Caps.Length);
    }

    [Fact]
    public void SelectedChoiceUsesAccentSurface()
    {
        ControlStyle style = UiThemeResolver.ResolveChoiceCell(selected: true, enabled: true, hovering: false);

        Assert.Equal(UiTheme.AccentDark, style.Fill);
        Assert.Equal(UiTheme.AccentBright, style.Text);
    }

    [Fact]
    public void DisabledStepperArrowUsesDimText()
    {
        ControlStyle style = UiThemeResolver.ResolveStepperArrow(enabled: false, hovering: false);

        Assert.Equal(UiTheme.Transparent, style.Fill);
        Assert.Equal(UiTheme.TextDim, style.Text);
    }
}
