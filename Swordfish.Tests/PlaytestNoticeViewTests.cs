using System;
using System.Collections.Generic;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using Xunit;

namespace Swordfish.Tests;

public class PlaytestNoticeViewTests
{
    [Fact]
    public void RendersFullModalFromMarkup()
    {
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), new UIController());
        var view = new PlaytestNoticeView(new StubLocalization());

        view.Render(ui);
        List<RenderCommand<Material>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "In-development playtest");
        Assert.Contains(commands, command => command.Text == "This is an evolving version.");
        Assert.Contains(commands, command => command.Text == "OK");
        Assert.Contains(commands, command => command.ID == "Button_Community");
        Assert.Contains(commands, command => command.ID == "Button_Wishlist");
        Assert.Contains(commands, command => command.ID == "Button_Close");

        RenderCommand<Material> scrim = FindRequired(commands, "PlaytestNoticeScrim");
        Assert.Equal(800, scrim.Rect.Size.X);
        Assert.Equal(600, scrim.Rect.Size.Y);
        Assert.Equal(0f, scrim.Color.X, 3);
        Assert.Equal(0.949f, scrim.Color.W, 3);

        RenderCommand<Material> panel = FindRequired(commands, "PlaytestNoticePanel");
        Assert.Equal(640, panel.Rect.Size.X);
        Assert.Equal(460, panel.Rect.Size.Y);

        RenderCommand<Material> close = FindRequired(commands, "Button_Close");
        Assert.Equal(200, close.Rect.Size.X);
        Assert.Equal(40, close.Rect.Size.Y);
        Assert.Equal(0.071f, close.BackgroundColor.X, 3);
        Assert.Equal(0.094f, close.BackgroundColor.Y, 3);
        Assert.Equal(0.149f, close.BackgroundColor.Z, 3);
        Assert.Equal(1, close.Border.Thickness);
        Assert.Equal(0.949f, close.Border.Color.X, 3);
        Assert.Equal(0.6f, close.Border.Color.Y, 3);
        Assert.Equal(0.29f, close.Border.Color.Z, 3);
        Assert.Equal(6, close.CornerCaps.Length);
        Assert.Equal(1, close.CornerCaps.Thickness);
        Assert.Equal(0.949f, close.CornerCaps.Color.X, 3);

        RenderCommand<Material> ok = commands.Find(command => command.Text == "OK");
        Assert.Equal(close.Rect.Position.X + 16, ok.Rect.Position.X);
        Assert.Equal(close.Rect.Position.Y + 8, ok.Rect.Position.Y);
        Assert.Equal(168, ok.Rect.Size.X);
        Assert.Equal(24, ok.Rect.Size.Y);
        Assert.Equal(close.Rect.Position.X + close.Rect.Size.X / 2, ok.Rect.Position.X + ok.Rect.Size.X / 2);
        Assert.Equal(close.Rect.Position.Y + close.Rect.Size.Y / 2, ok.Rect.Position.Y + ok.Rect.Size.Y / 2);
        Assert.Equal(HorizontalAlignment.Center, ok.FontOptions.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, ok.FontOptions.VerticalAlignment);
    }

    [Fact]
    public void ModalUsesMaxSizeOnLargeWindow()
    {
        var ui = new UIBuilder<Material>(1920, 1080, new StubTextEngine(), new UIController());
        var view = new PlaytestNoticeView(new StubLocalization());

        view.Render(ui);
        List<RenderCommand<Material>> commands = ui.Build(0d);

        RenderCommand<Material> panel = FindRequired(commands, "PlaytestNoticePanel");
        Assert.Equal(760, panel.Rect.Size.X);
        Assert.Equal(500, panel.Rect.Size.Y);
    }

    [Fact]
    public void CloseAndBrandButtonsForwardInteractions()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), controller);
        var view = new PlaytestNoticeView(new StubLocalization());
        var closeCount = 0;
        var discordCount = 0;
        var steamCount = 0;
        view.CloseRequested = () => closeCount++;
        view.DiscordRequested = () => discordCount++;
        view.SteamRequested = () => steamCount++;

        view.Render(ui);
        ui.Build(0d);

        Click(controller, ui, view, "Button_Close");
        Click(controller, ui, view, "Button_Community");
        Click(controller, ui, view, "Button_Wishlist");

        Assert.Equal(1, closeCount);
        Assert.Equal(1, discordCount);
        Assert.Equal(1, steamCount);
    }

    private static void Click(
        UIController controller,
        UIBuilder<Material> ui,
        PlaytestNoticeView view,
        string id)
    {
        ElementBounds bounds = ui.GetElementBounds(id);
        int x = bounds.Rect.Left + Math.Max(1, bounds.Rect.Size.X / 2);
        int y = bounds.Rect.Top + Math.Max(1, bounds.Rect.Size.Y / 2);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui);
        ui.Build(0.1d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui);
        ui.Build(0.1d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui);
        ui.Build(0.1d);
    }

    private sealed class StubLocalization : ILocalization
    {
        public string? GetString(string value)
        {
            return value switch
            {
                "ui.dialog.playtest" => "In-development playtest",
                "ui.dialog.playtest.body" => "This is an evolving version.",
                "ui.button.ok" => "OK",
                _ => value,
            };
        }

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, System.Globalization.CultureInfo cultureInfo) => GetString(value);
    }

    private static RenderCommand<Material> FindRequired(
        List<RenderCommand<Material>> commands,
        string id)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].ID == id)
            {
                return commands[i];
            }
        }

        throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
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
        {
            return MeasureText(fontOptions, text.Substring(start, length));
        }

        public TextLayout Layout(FontOptions fontOptions, string text) => LayoutText(fontOptions, text);

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => LayoutText(fontOptions, text);

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length)
        {
            return LayoutText(fontOptions, text.Substring(start, length));
        }

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
        {
            return LayoutText(fontOptions, text.Substring(start, length));
        }

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [text];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth)
        {
            return [text.Substring(start, length)];
        }

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
