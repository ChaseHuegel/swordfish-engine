using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
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

    private sealed class StubTextEngine : ITextEngine
    {
        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => default;

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => default;

        public TextLayout Layout(FontOptions fontOptions, string text) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => TextLayout.Empty;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => [];
    }
}
