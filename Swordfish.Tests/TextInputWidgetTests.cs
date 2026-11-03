using System;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Controls;
using Xunit;

namespace Swordfish.Tests;

public class TextInputWidgetTests
{
    [Fact]
    public void WidgetUsesStyleForFrameAndPaletteForPlaceholder()
    {
        var ui = new UIBuilder<Material>(200, 80, new StubTextEngine(), new UIController());
        var state = new TextBoxState("hello");
        var style = new Style
        {
            Color = new Vector4(1f, 1f, 1f, 1f),
            BackgroundColor = new Vector4(0.1f, 0.2f, 0.3f, 1f),
            Border = new BorderStyle
            {
                Thickness = 1,
                Color = new Vector4(0.4f, 0.5f, 0.6f, 1f),
            },
        };
        var palette = new TextInputPalette(
            Text: new Vector4(1f, 1f, 1f, 1f),
            TextFocused: new Vector4(1f, 1f, 1f, 1f),
            Placeholder: new Vector4(0.5f, 0.5f, 0.5f, 1f),
            PlaceholderFocused: new Vector4(0.5f, 0.5f, 0.5f, 1f),
            Selection: new Vector4(0f, 0.5f, 1f, 0.5f),
            Caret: new Vector4(1f, 1f, 1f, 1f)
        );

        TextInputWidget.TextInput(
            ui,
            "input",
            ref state,
            "type here",
            new FakeInputService(),
            style,
            new FontOptions { Size = 16 },
            new Constraints { Width = new Reef.Constraints.Fixed(160), Height = new Reef.Constraints.Fixed(32) },
            palette
        );

        List<RenderCommand<Material>> commands = ui.Build(0d);
        RenderCommand<Material> input = commands.Find(command => command.ID == "input");
        Assert.Equal(0.1f, input.BackgroundColor.X, 3);
        Assert.Equal(1, input.Border.Thickness);
        Assert.Equal(0.4f, input.Border.Color.X, 3);

        RenderCommand<Material> text = commands.Find(command => command.Text == "hello");
        Assert.Equal(1f, text.Color.X, 3);
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
            int height = Math.Max(1, fontOptions.Size);
            return new TextConstraints(width, height, width, height);
        }

        private static TextLayout LayoutText(FontOptions fontOptions, string text)
        {
            return new TextLayout(MeasureText(fontOptions, text), [], [], Math.Max(1, fontOptions.Size));
        }
    }
}
