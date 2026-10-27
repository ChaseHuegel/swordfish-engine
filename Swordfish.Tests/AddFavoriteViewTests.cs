using System;
using System.Collections.Generic;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using Xunit;

namespace Swordfish.Tests;

public class AddFavoriteViewTests
{
    [Fact]
    public void RendersFieldsAndForwardsActions()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), controller);
        var view = new AddFavoriteView(new StubLocalization(), new FakeInputService());
        var state = new AddFavoriteState(new TextBoxState(string.Empty), new TextBoxState(string.Empty));
        var submitCount = 0;
        var cancelCount = 0;
        view.SubmitRequested = () => submitCount++;
        view.CancelRequested = () => cancelCount++;

        view.Render(ui, state);
        List<RenderCommand<Material>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Add signal");
        Assert.Contains(commands, command => command.ID == "TextBox_FavoriteHost");
        Assert.Contains(commands, command => command.ID == "TextBox_FavoritePort");
        Assert.Contains(commands, command => command.ID == "Button_SubmitFavorite");
        Assert.Contains(commands, command => command.ID == "Button_CancelFavorite");

        Click(controller, ui, view, state, "Button_SubmitFavorite");
        Click(controller, ui, view, state, "Button_CancelFavorite");

        Assert.Equal(1, submitCount);
        Assert.Equal(1, cancelCount);
    }

    private static void Click(
        UIController controller,
        UIBuilder<Material> ui,
        AddFavoriteView view,
        AddFavoriteState state,
        string id)
    {
        ElementBounds bounds = ui.GetElementBounds(id);
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
    }

    private sealed class StubLocalization : Swordfish.Library.Globalization.ILocalization
    {
        public string? GetString(string value)
        {
            return value switch
            {
                "ui.dialog.server.add" => "Add signal",
                _ => value,
            };
        }

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, System.Globalization.CultureInfo cultureInfo) => GetString(value);
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
