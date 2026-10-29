using System;
using System.Collections.Generic;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using Xunit;

namespace Swordfish.Tests;

public class TextBoxModifierTests
{
    [Fact]
    public void ModifierKeysClearOnRelease()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(200, 100, new StubTextEngine(), controller);
        var state = new TextBoxState("Hello");

        Render(ui, ref state);
        ElementBounds bounds = ui.GetElementBounds("test");
        Click(controller, ui, ref state, bounds.Rect.Left + 5, bounds.Rect.Top + 5);

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Shift, pressed: true)]);
        Render(ui, ref state);
        Assert.True(state.ShiftModifier);

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Shift, pressed: false)]);
        Render(ui, ref state);
        Assert.False(state.ShiftModifier);

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Control, pressed: true)]);
        Render(ui, ref state);
        Assert.True(state.ControlModifier);

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Control, pressed: false)]);
        Render(ui, ref state);
        Assert.False(state.ControlModifier);
    }

    [Fact]
    public void ModifiersClearWhenFocusIsLost()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(200, 100, new StubTextEngine(), controller);
        var state = new TextBoxState("Hello");

        Render(ui, ref state);
        ElementBounds bounds = ui.GetElementBounds("test");
        Click(controller, ui, ref state, bounds.Rect.Left + 5, bounds.Rect.Top + 5);

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Shift, pressed: true)]);
        Render(ui, ref state);
        Assert.True(state.ShiftModifier);

        ui.Unfocus();
        Render(ui, ref state);
        Assert.False(state.ShiftModifier);

        state.ControlModifier = true;
        ui.Focus("test");
        Render(ui, ref state);
        Assert.False(state.ControlModifier);
    }

    private static void Click(
        UIController controller,
        UIBuilder<Material> ui,
        ref TextBoxState state,
        int x,
        int y)
    {
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        Render(ui, ref state);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        Render(ui, ref state);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        Render(ui, ref state);
    }

    private static void Render(UIBuilder<Material> ui, ref TextBoxState state)
    {
        ui.TextBox("test", ref state, new FontOptions { Size = 16 }, new FakeInputService(), out _);
        ui.Build(0.1d);
    }

    private sealed class StubTextEngine : ITextEngine
    {
        private static readonly TextLayout _layout = CreateLayout();

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

        private static TextLayout CreateLayout()
        {
            var glyphs = new GlyphLayout[5];
            for (var i = 0; i < glyphs.Length; i++)
            {
                glyphs[i] = new GlyphLayout(
                    new IntRect(i * 10, 0, (i + 1) * 10, 20),
                    new IntRect(0, 0, 1, 1)
                );
            }

            return new TextLayout(
                new TextConstraints(50, 20, 50, 20),
                glyphs,
                ["Hello"],
                lineHeight: 20
            );
        }
    }
}
