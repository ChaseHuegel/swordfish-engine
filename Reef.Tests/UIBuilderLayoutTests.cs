using System.Linq;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Xunit;

namespace Reef.Tests;

public class UIBuilderLayoutTests
{
    [Fact]
    public void CenterAnchorsWorkInANoneLayoutParent()
    {
        var ui = new UIBuilder<object>(400, 200, new StubTextEngine(), new UIController());

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Reef.UI.Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            using (ui.Element("child"))
            {
                ui.Constraints = new Reef.UI.Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(100),
                    Height = new Fixed(60),
                };
            }
        }

        RenderCommand<object> child = ui.Build(0f).Single(command => command.ID == "child");

        Assert.Equal(150, child.Rect.Left);
        Assert.Equal(70, child.Rect.Top);
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
