using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

public class UIControllerClipTests
{
    [Fact]
    public void ClippedControlDoesNotReceiveHoverOrClickOutsideClip()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        controller.UpdateMouse(10, 40, UIController.MouseButtons.None);
        var clicked = false;
        RenderClippedControl(ui, ref clicked);
        ui.Build(delta: 0f);

        controller.UpdateMouse(10, 40, UIController.MouseButtons.Left);
        RenderClippedControl(ui, ref clicked);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(10, 40, UIController.MouseButtons.None);
        RenderClippedControl(ui, ref clicked);
        ui.Build(delta: 0.1f);

        Assert.False(clicked);
    }

    [Fact]
    public void ControlInsideClipReceivesClick()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        var clicked = false;
        RenderClippedControl(ui, ref clicked);
        ui.Build(delta: 0f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderClippedControl(ui, ref clicked);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderClippedControl(ui, ref clicked);
        ui.Build(delta: 0.1f);

        Assert.True(clicked);
    }

    private static void RenderClippedControl(UIBuilder<PixelTexture> ui, ref bool clicked)
    {
        using (ui.Element("clip"))
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(100),
            };
            ui.ClipConstraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(20),
            };

            using (ui.Element("clipped"))
            {
                ui.Color = new System.Numerics.Vector4(1f);
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(50),
                    Height = new Fixed(50),
                };

                clicked = ui.Clicked();
            }
        }
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
