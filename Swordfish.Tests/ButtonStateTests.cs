using System;
using System.Collections.Generic;
using System.Globalization;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class ButtonStateTests
{
    [Fact]
    public void DisabledButtonKeepsDisabledStyleWhileHoveredAndPressed()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(264, 44, new StubTextEngine(), controller);
        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);

        Render(ui);
        List<RenderCommand<Material>> commands = ui.Build(0d);
        AssertDisabled(commands[0]);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        Render(ui);
        commands = ui.Build(0d);
        AssertDisabled(commands[0]);
    }

    [Fact]
    public void DisabledFeedbackSubmitDoesNotFire()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), controller);
        var view = new FeedbackView(new StubLocalization(), new FakeInputService());
        var state = new FeedbackState(new TextBoxState(string.Empty), new TextBoxState(string.Empty));
        var submitted = 0;
        view.SubmitRequested = () => submitted++;

        view.Render(ui, state);
        ui.Build(0.1d);
        ElementBounds bounds = ui.GetElementBounds("Button_Submit");
        int x = bounds.Rect.Left + Math.Max(1, bounds.Rect.Size.X / 2);
        int y = bounds.Rect.Top + Math.Max(1, bounds.Rect.Size.Y / 2);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, state);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.1d);

        Assert.Equal(0, submitted);
    }

    private static void Render(UIBuilder<Material> ui)
    {
        new Button().Render(
            ui,
            parameters: new ButtonParameters(Enabled: false),
            callerConstraints: new ConstraintsOverride
            {
                Width = new Fixed(264),
                Height = new Fixed(44),
            },
            content_default: content =>
            {
                using (content.Text("Disabled"))
                {
                    content.FontOptions = new FontOptions { Size = 18 };
                    content.Constraints = new Constraints { Anchors = Anchors.Center };
                }
            }
        );
    }

    private static void AssertDisabled(RenderCommand<Material> command)
    {
        Assert.Equal(UiTheme.PanelInner.X, command.Color.X, 3);
        Assert.Equal(UiTheme.PanelInner.Y, command.Color.Y, 3);
        Assert.Equal(UiTheme.PanelInner.Z, command.Color.Z, 3);
        Assert.Equal(UiTheme.BorderInner.X, command.Border.Color.X, 3);
        Assert.Equal(0, command.CornerCaps.Length);
    }

    private sealed class StubLocalization : ILocalization
    {
        public string? GetString(string value) => value;

        public string? GetString(string value, string cultureName) => value;

        public string? GetString(string value, CultureInfo cultureInfo) => value;
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

        public TextConstraints Measure(FontOptions fontOptions, string text) => MeasureText(fontOptions, text);

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
            => MeasureText(fontOptions, text.Substring(start, length));

        public TextLayout Layout(FontOptions fontOptions, string text) => LayoutText(fontOptions, text);

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => LayoutText(fontOptions, text);

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length)
            => LayoutText(fontOptions, text.Substring(start, length));

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
            => LayoutText(fontOptions, text.Substring(start, length));

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [text];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth)
            => [text.Substring(start, length)];

        private static TextConstraints MeasureText(FontOptions fontOptions, string text)
        {
            int width = text.Length * 8;
            int height = System.Math.Max(1, fontOptions.Size);
            return new TextConstraints(width, height, width, height);
        }

        private static TextLayout LayoutText(FontOptions fontOptions, string text)
        {
            return new TextLayout(MeasureText(fontOptions, text), [], [], System.Math.Max(1, fontOptions.Size));
        }
    }
}
