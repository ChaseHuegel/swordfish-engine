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

public class ActionButtonTests
{
    [Fact]
    public void FixedActionButtonUsesRequestedRect()
    {
        UIBuilder<Material> ui = CreateBuilder();
        ui.ActionButton("fixed", Widgets.Rect(10, 20, 120, 30), "OK", ButtonVariant.Secondary);

        RenderCommand<Material> button = Find(ui.Build(0f), "fixed");

        Assert.Equal(10, button.Rect.Left);
        Assert.Equal(20, button.Rect.Top);
        Assert.Equal(120, button.Rect.Size.X);
        Assert.Equal(30, button.Rect.Size.Y);
    }

    [Fact]
    public void MenuActionButtonUsesSharedIntrinsicSize()
    {
        UIBuilder<Material> ui = CreateBuilder();
        ui.ActionButton(
            "menu",
            "OK",
            Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService: null!)
        );

        RenderCommand<Material> button = Find(ui.Build(0f), "menu");

        Assert.Equal(UiMetrics.MenuButtonMinWidth, button.Rect.Size.X);
        Assert.Equal(UiMetrics.MenuButtonHeight, button.Rect.Size.Y);
    }

    [Fact]
    public void FontAwesomeIconIsVerticallyCentered()
    {
        UIBuilder<Material> ui = CreateBuilder();
        var options = new Widgets.IconButtonOptions(
            FontSize: 32
        );

        ui.IconButton("icon", Widgets.Rect(10, 20, 40, 40), "\uf2ed", options);

        RenderCommand<Material> text = ui.Build(0f).Single(command => command.Text == "\uf2ed");

        Assert.Equal(14, text.Rect.Left);
        Assert.Equal(20, text.Rect.Top);
    }

    private static UIBuilder<Material> CreateBuilder()
    {
        return new UIBuilder<Material>(400, 200, new StubTextEngine(), new UIController());
    }

    private static RenderCommand<Material> Find(List<RenderCommand<Material>> commands, string id)
    {
        return commands.Single(command => command.ID == id);
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
