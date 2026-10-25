#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class TextBoxZOrderTests
{
    private static readonly Vector4 SELECTION_COLOR = UiTheme.RowSelected;

    [Fact]
    public void SelectionPaintsBehindText()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(200, 50, new StubTextEngine(), controller);
        var state = new TextBoxState("Hello");

        RenderTextBox(ui, ref state);
        ui.Build(delta: 0f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderTextBox(ui, ref state);
        ui.Build(delta: 0.1d);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderTextBox(ui, ref state);
        ui.Build(delta: 0.1d);

        state.SelectionStartIndex = 0;
        state.CaretIndex = 3;
        List<RenderCommand<Material>> commands = RenderAndBuild(ui, ref state, delta: 0.1d);

        int selectionIndex = FindColor(commands, SELECTION_COLOR);
        int textIndex = FindId(commands, "test_Text");

        Assert.True(selectionIndex >= 0);
        Assert.True(textIndex >= 0);
        Assert.True(selectionIndex < textIndex);
    }

    [Fact]
    public void CaretPaintsOverText()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(200, 50, new StubTextEngine(), controller);
        var state = new TextBoxState("Hello");

        RenderTextBox(ui, ref state);
        ui.Build(delta: 0f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderTextBox(ui, ref state);
        ui.Build(delta: 0.1d);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderTextBox(ui, ref state);
        ui.Build(delta: 0.1d);

        state.SelectionStartIndex = 3;
        state.CaretIndex = 3;
        List<RenderCommand<Material>> commands = RenderAndBuild(ui, ref state, delta: 0.1d);

        int textIndex = FindId(commands, "test_Text");
        int caretIndex = FindCaret(commands);

        Assert.True(textIndex >= 0);
        Assert.True(caretIndex >= 0);
        Assert.True(textIndex < caretIndex);
    }

    private static List<RenderCommand<Material>> RenderAndBuild(UIBuilder<Material> ui, ref TextBoxState state, double delta)
    {
        RenderTextBox(ui, ref state);
        return ui.Build(delta);
    }

    private static void RenderTextBox(UIBuilder<Material> ui, ref TextBoxState state)
    {
        ui.TextBox(
            id: "test",
            ref state,
            new FontOptions { Size = 20 },
            new FakeInputService(),
            out _
        );
    }

    private static int FindId(List<RenderCommand<Material>> commands, string id)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].ID == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindColor(List<RenderCommand<Material>> commands, Vector4 color)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            Vector4 commandColor = commands[i].Color;
            if (commandColor == color)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindCaret(List<RenderCommand<Material>> commands)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            RenderCommand<Material> command = commands[i];
            if (command.ID == null && command.Text == null && command.Rect.Size.X == 1 && command.Color == UiTheme.Text)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class StubTextEngine : ITextEngine
    {
        private readonly TextLayout _layout;

        public StubTextEngine()
        {
            var glyphs = new GlyphLayout[5];
            for (var i = 0; i < glyphs.Length; i++)
            {
                glyphs[i] = new GlyphLayout(
                    new IntRect(i * 10, 0, (i + 1) * 10, 20),
                    new IntRect(0, 0, 1, 1)
                );
            }

            _layout = new TextLayout(
                new TextConstraints(50, 20, 50, 20),
                glyphs,
                ["Hello"],
                lineHeight: 20
            );
        }

        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => new(50, 20, 50, 20);

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => new(50, 20, 50, 20);

        public TextLayout Layout(FontOptions fontOptions, string text) => _layout;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => _layout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => _layout;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => _layout;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => ["Hello"];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => ["Hello"];
    }
}
