using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class MenuPanelRenderTests
{
    [Fact]
    public void MenuPanelHasNoOuterBorder()
    {
        var ui = new UIBuilder<Material>(200, 200, new StubTextEngine(), new UIController());

        using (ui.MenuPanel(Widgets.Rect(0, 0, 100, 100))) { }

        List<RenderCommand<Material>> commands = ui.Build(0f);
        RenderCommand<Material> root = commands.Single(command => command.Rect.Size.X == 100 && command.Rect.Size.Y == 100);
        RenderCommand<Material> inner = commands.Single(command => command.Rect.Size.X == 96 && command.Rect.Size.Y == 96);

        Assert.Equal(0, root.Border.Thickness);
        Assert.Equal(1, inner.Border.Thickness);
    }

    [Fact]
    public void InfoBoxSeparatesColorTokenFromHighlight()
    {
        var ui = new UIBuilder<Material>(200, 200, new StubTextEngine(), new UIController());

        ui.InfoBox(Widgets.Rect(0, 0, 200, 100), "Info", "Body.", "Awareness:");

        List<RenderCommand<Material>> commands = ui.Build(0f);
        Assert.True(
            commands.Exists(command => command.Text?.Contains("#F2994AFF") == true),
            string.Join("\n", commands.Select(command => $"{command.ID ?? "-"} {command.Text ?? "<null>"}"))
        );
        RenderCommand<Material> text = commands.Single(
            command => command.Text?.Contains("#F2994AFF") == true
        );

        Assert.Equal("#F2994AFF Awareness: #R Body.", text.Text);
    }

    [Fact]
    public void StatLabelMarkupKeepsVisibleText()
    {
        const string focused = "#orangeS#Rtrength";
        const string unfocused = "#cyanA#Rwareness";

        Assert.Equal("Strength", RichText.Parse(focused, 0, focused.Length).Text);
        Assert.Equal("Awareness", RichText.Parse(unfocused, 0, unfocused.Length).Text);
    }

    [Fact]
    public void StatLabelEmphasizesFirstLetter()
    {
        var ui = new UIBuilder<Material>(200, 200, new StubTextEngine(), new UIController());

        ui.StatLabel("Strength", Widgets.Rect(0, 0, 200, 20), 14, focused: true);
        ui.StatLabel("Awareness", Widgets.Rect(0, 30, 200, 20), 14, focused: false);

        List<RenderCommand<Material>> commands = ui.Build(0f);

        Assert.Contains(commands, command => command.Text == "#orangeS#Rtrength");
        Assert.Contains(commands, command => command.Text == "#cyanA#Rwareness");
    }

    [Fact]
    public void ModalPanelHasNoBorder()
    {
        var ui = new UIBuilder<Material>(200, 200, new StubTextEngine(), new UIController());

        using (ui.ModalPanel(Widgets.Rect(0, 0, 100, 100))) { }

        List<RenderCommand<Material>> commands = ui.Build(0f);
        RenderCommand<Material> root = commands.Single(command => command.Rect.Size.X == 100 && command.Rect.Size.Y == 100);

        Assert.Equal(0, root.Border.Thickness);
        Assert.DoesNotContain(
            commands,
            command => command.Rect.Size.X == 96 && command.Rect.Size.Y == 96
        );
    }

    [Fact]
    public void UnselectedCardUsesOneBorder()
    {
        var ui = new UIBuilder<Material>(200, 200, new StubTextEngine(), new UIController());

        ui.SelectableCard(
            "card",
            Widgets.Rect(10, 10, 100, 40),
            new Widgets.SelectableCardOptions(Selected: false),
            _ => { }
        );

        List<RenderCommand<Material>> commands = ui.Build(0f);
        RenderCommand<Material> card = commands.Single(
            command => command.Rect.Size.X == 100 && command.Rect.Size.Y == 40
        );

        Assert.Equal(1, card.Border.Thickness);
        Assert.Equal(UiTheme.Border, card.Border.Color);
        Assert.DoesNotContain(
            commands,
            command => command.Rect.Size.X == 96 && command.Rect.Size.Y == 36
        );
        Assert.DoesNotContain(commands, command => command.Color == UiTheme.Accent);
    }

    [Fact]
    public void SelectedCardUsesOneBorderAndACyanBar()
    {
        var ui = new UIBuilder<Material>(200, 200, new StubTextEngine(), new UIController());

        ui.SelectableCard(
            "card",
            Widgets.Rect(10, 10, 100, 40),
            new Widgets.SelectableCardOptions(Selected: true),
            _ => { }
        );

        List<RenderCommand<Material>> commands = ui.Build(0f);
        RenderCommand<Material> card = commands.Single(command => command.ID == "card");

        Assert.Equal(1, card.Border.Thickness);
        Assert.Equal(UiTheme.Primary, card.Border.Color);
        Assert.Contains(
            commands,
            command => command.Color == UiTheme.Accent
                && command.Rect.Size.X == 3
                && command.Rect.Size.Y == 28
        );
        Assert.DoesNotContain(
            commands,
            command => command.Rect.Size.X == 96 && command.Rect.Size.Y == 36
        );
    }

    private sealed class StubTextEngine : ITextEngine
    {
        private static readonly TextLayout CachedLayout = new(
            new TextConstraints(32, 32, 32, 32),
            [],
            ["OK"],
            lineHeight: 20
        );

        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => new(32, 32, 32, 32);

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => new(32, 32, 32, 32);

        public TextLayout Layout(FontOptions fontOptions, string text) => CachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => CachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => CachedLayout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => CachedLayout;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => ["OK"];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => ["OK"];
    }
}
