using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Layers.Menus.Pause;
using Xunit;

namespace Swordfish.Tests;

public class UiSnapshotTests
{
    private static readonly int[] _widths = [800, 1280, 1920];
    private static readonly int[] _heights = [600, 720, 1080];

    [Fact]
    public void MigratedViewsMatchSnapshots()
    {
        for (var i = 0; i < _widths.Length; i++)
        {
            int width = _widths[i];
            int height = _heights[i];
            Compare($"{width}x{height}-playtest", width, height, (ui) =>
            {
                new PlaytestNoticeView(new StubLocalization()).Render(ui);
            });
            Compare($"{width}x{height}-pause-home", width, height, (ui) =>
            {
                new HomePageView(new StubLocalization()).Render(ui);
            });
            Compare($"{width}x{height}-message", width, height, (ui) =>
            {
                new MessageModalView(new StubLocalization()).Render(
                    ui,
                    new MessageModalState("Message", "This is a message.")
                );
            });
            Compare($"{width}x{height}-confirm", width, height, (ui) =>
            {
                new ConfirmModalView(new StubLocalization()).Render(
                    ui,
                    new ConfirmModalState("Confirm", "Are you sure?", "Confirm", "Cancel")
                );
            });
            Compare($"{width}x{height}-add-favorite", width, height, (ui) =>
            {
                var state = new AddFavoriteState(
                    new TextBoxState(string.Empty),
                    new TextBoxState("7777")
                );
                new AddFavoriteView(new StubLocalization(), new FakeInputService()).Render(ui, state);
            });
            Compare($"{width}x{height}-feedback", width, height, (ui) =>
            {
                var state = new FeedbackState(
                    new TextBoxState(string.Empty),
                    new TextBoxState(string.Empty)
                );
                new FeedbackView(new StubLocalization(), new FakeInputService()).Render(ui, state);
            });
            Compare($"{width}x{height}-connecting", width, height, (ui) =>
            {
                new ConnectingModalView(new StubLocalization()).Render(
                    ui,
                    new ConnectingModalState("Connecting", "127.0.0.1:7777")
                );
            });
            Compare($"{width}x{height}-main-menu-overlay", width, height, (ui) =>
            {
                new MainMenuOverlayView(new StubLocalization()).Render(ui);
            });
            Compare($"{width}x{height}-credits", width, height, (ui) =>
            {
                var scroll = new WaywardBeyond.Client.UI.Components.ScrollAreaState();
                new CreditsView(new StubLocalization(), scroll).Render(ui);
            });
        }
    }

    [Fact]
    public void MigratedPagesMatchLegacyVisuals()
    {
        for (var i = 0; i < _widths.Length; i++)
        {
            int width = _widths[i];
            int height = _heights[i];
            AssertPageMatchesLegacy($"{width}x{height}-playtest", width, height, ui =>
            {
                new PlaytestNoticeView(new StubLocalization()).Render(ui);
            });
            AssertPageMatchesLegacy($"{width}x{height}-pause-home", width, height, ui =>
            {
                new HomePageView(new StubLocalization()).Render(ui);
            });
            AssertPageMatchesLegacy($"{width}x{height}-message", width, height, ui =>
            {
                new MessageModalView(new StubLocalization()).Render(
                    ui,
                    new MessageModalState("Message", "This is a message.")
                );
            });
            AssertPageMatchesLegacy($"{width}x{height}-confirm", width, height, ui =>
            {
                new ConfirmModalView(new StubLocalization()).Render(
                    ui,
                    new ConfirmModalState("Confirm", "Are you sure?", "Confirm", "Cancel")
                );
            });
            AssertPageMatchesLegacy($"{width}x{height}-add-favorite", width, height, ui =>
            {
                var state = new AddFavoriteState(
                    new TextBoxState(string.Empty),
                    new TextBoxState("7777")
                );
                new AddFavoriteView(new StubLocalization(), new FakeInputService()).Render(ui, state);
            });
            AssertPageMatchesLegacy($"{width}x{height}-feedback", width, height, ui =>
            {
                var state = new FeedbackState(
                    new TextBoxState(string.Empty),
                    new TextBoxState(string.Empty)
                );
                new FeedbackView(new StubLocalization(), new FakeInputService()).Render(ui, state);
            });
            AssertPageMatchesLegacy($"{width}x{height}-connecting", width, height, ui =>
            {
                new ConnectingModalView(new StubLocalization()).Render(
                    ui,
                    new ConnectingModalState("Connecting", "127.0.0.1:7777")
                );
            });
            AssertPageMatchesLegacy($"{width}x{height}-credits", width, height, ui =>
            {
                var scroll = new WaywardBeyond.Client.UI.Components.ScrollAreaState();
                new CreditsView(new StubLocalization(), scroll).Render(ui);
            });
            AssertPageMatchesLegacy($"{width}x{height}-main-menu-overlay", width, height, ui =>
            {
                new MainMenuOverlayView(new StubLocalization()).Render(ui);
            });
        }
    }

    internal static void AssertMatchesLegacy(string name, string actualRaw)
    {
        string legacyPath = Path.Combine(SourceDirectory(), "TestFiles", "Legacy", "Pages", name + ".txt");
        string legacy = NormalizeVisual(File.ReadAllText(legacyPath));
        string actual = NormalizeVisual(actualRaw);
        Assert.True(
            string.Equals(legacy, actual, StringComparison.Ordinal),
            $"Legacy visual mismatch for {name}."
        );
    }

    internal static void AssertPageMatchesLegacy(
        string name,
        int width,
        int height,
        Action<UIBuilder<Material>> render)
    {
        string legacyPath = Path.Combine(SourceDirectory(), "TestFiles", "Legacy", "Pages", name + ".txt");
        string legacy = NormalizeVisual(File.ReadAllText(legacyPath));
        string actual = NormalizeVisual(CaptureRender(width, height, render));
        Assert.True(
            string.Equals(legacy, actual, StringComparison.Ordinal),
            $"Legacy visual mismatch for {name}."
        );
    }

    internal static void Compare(
        string name,
        int width,
        int height,
        Action<UIBuilder<Material>> render,
        string folder = "Snapshots",
        bool allowUpdate = true)
    {
        var ui = new UIBuilder<Material>(width, height, new StubTextEngine(), new UIController());
        render(ui);
        string actual = Capture(ui.Build(0d));
        string goldenPath = Path.Combine(SourceDirectory(), "TestFiles", folder, name + ".txt");
        string actualPath = Path.Combine(AppContext.BaseDirectory, "TestResults", "ui", name + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(actualPath)!);
        File.WriteAllText(actualPath, actual);

        bool update = string.Equals(
            Environment.GetEnvironmentVariable("UI_SNAPSHOT_UPDATE"),
            "1",
            StringComparison.Ordinal
        );
        if (!File.Exists(goldenPath) || (update && allowUpdate))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actual);
            return;
        }

        string expected = File.ReadAllText(goldenPath);
        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            $"Snapshot mismatch for {name}. Actual: {actualPath}. Golden: {goldenPath}."
        );
    }

    internal static string CaptureRender(int width, int height, Action<UIBuilder<Material>> render)
    {
        var ui = new UIBuilder<Material>(width, height, new StubTextEngine(), new UIController());
        render(ui);
        return Capture(ui.Build(0d));
    }

    internal static string NormalizeIds(string snapshot)
    {
        return Regex.Replace(snapshot, @" id=[^ ]+", " id=-");
    }

    private static bool IsFullyClipped(string line)
    {
        Match rect = Regex.Match(line, @" rect=(-?\d+),(-?\d+),(\d+),(\d+)");
        Match clip = Regex.Match(line, @" clip=(-?\d+),(-?\d+),(\d+),(\d+)");
        if (!rect.Success || !clip.Success)
        {
            return false;
        }

        int left = int.Parse(rect.Groups[1].Value, CultureInfo.InvariantCulture);
        int top = int.Parse(rect.Groups[2].Value, CultureInfo.InvariantCulture);
        int right = left + int.Parse(rect.Groups[3].Value, CultureInfo.InvariantCulture);
        int bottom = top + int.Parse(rect.Groups[4].Value, CultureInfo.InvariantCulture);
        int clipLeft = int.Parse(clip.Groups[1].Value, CultureInfo.InvariantCulture);
        int clipTop = int.Parse(clip.Groups[2].Value, CultureInfo.InvariantCulture);
        int clipRight = clipLeft + int.Parse(clip.Groups[3].Value, CultureInfo.InvariantCulture);
        int clipBottom = clipTop + int.Parse(clip.Groups[4].Value, CultureInfo.InvariantCulture);

        return right <= clipLeft || left >= clipRight || bottom <= clipTop || top >= clipBottom;
    }

    internal static string CaptureHovered(
        int width,
        int height,
        Action<UIBuilder<Material>> render,
        string hoverId)
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(width, height, new StubTextEngine(), controller);
        render(ui);
        ui.Build(0d);
        ElementBounds bounds = ui.GetElementBounds(hoverId);
        int x = bounds.Rect.Left + Math.Max(1, bounds.Rect.Size.X / 2);
        int y = bounds.Rect.Top + Math.Max(1, bounds.Rect.Size.Y / 2);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        render(ui);
        ui.Build(0d);
        render(ui);
        return Capture(ui.Build(0d));
    }

    internal static string NormalizeVisual(string snapshot)
    {
        string withoutClipped = string.Join(
            "\n",
            NormalizeIds(snapshot).Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line => !IsFullyClipped(line)));
        string normalized = Regex.Replace(withoutClipped, @" clip=[^ ]+", " clip=-");
        normalized = Regex.Replace(normalized, @"color=([^ ]+) bg=\1", @"color=$1 bg=0,0,0,0");
        string[] lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Contains(" color=0,0,0,0 bg=0,0,0,0 border=0:0,0,0,0 caps=0:0:0,0,0,0 radius=0:0:0:0 font=-:0 text=-", StringComparison.Ordinal))
            {
                continue;
            }

            int space = line.IndexOf(' ');
            kept.Add(kept.Count.ToString(CultureInfo.InvariantCulture) + line.Substring(space));
        }

        return string.Join("\n", kept) + "\n";
    }

    internal static string Capture(List<RenderCommand<Material>> commands)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < commands.Count; i++)
        {
            RenderCommand<Material> command = commands[i];
            string id = command.ID != null && !command.ID.Contains("__rml_", StringComparison.Ordinal)
                ? command.ID
                : "-";
            builder.Append(i.ToString(CultureInfo.InvariantCulture));
            builder.Append(" id=").Append(id);
            builder.Append(" rect=").Append(Format(command.Rect));
            builder.Append(" clip=").Append(Format(command.ClipRect));
            builder.Append(" color=").Append(Format(command.Color));
            builder.Append(" bg=").Append(Format(command.BackgroundColor));
            builder.Append(" border=").Append(command.Border.Thickness.ToString(CultureInfo.InvariantCulture));
            builder.Append(':').Append(Format(command.Border.Color));
            builder.Append(" caps=").Append(command.CornerCaps.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':').Append(command.CornerCaps.Thickness.ToString(CultureInfo.InvariantCulture));
            builder.Append(':').Append(Format(command.CornerCaps.Color));
            builder.Append(" radius=").Append(command.CornerRadius.TopLeft.ToString(CultureInfo.InvariantCulture));
            builder.Append(':').Append(command.CornerRadius.TopRight.ToString(CultureInfo.InvariantCulture));
            builder.Append(':').Append(command.CornerRadius.BottomLeft.ToString(CultureInfo.InvariantCulture));
            builder.Append(':').Append(command.CornerRadius.BottomRight.ToString(CultureInfo.InvariantCulture));
            builder.Append(" font=").Append(command.FontOptions.ID ?? "-");
            builder.Append(':').Append(command.FontOptions.Size.ToString(CultureInfo.InvariantCulture));
            builder.Append(" text=").Append(command.Text ?? "-");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Format(IntRect rect)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{rect.Left},{rect.Top},{rect.Size.X},{rect.Size.Y}"
        );
    }

    private static string Format(Vector4 color)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{color.X:0.####},{color.Y:0.####},{color.Z:0.####},{color.W:0.####}"
        );
    }

    private static string SourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
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
                "ui.button.pause.continue" => "Continue",
                "ui.button.settings" => "Settings",
                "ui.button.feedback.send" => "Send feedback",
                "ui.button.pause.saveExit" => "Save and exit",
                "ui.button.cancel" => "Cancel",
                "ui.dialog.server.add" => "Add signal",
                "ui.field.server.host" => "Host",
                "ui.field.server.port" => "Port",
                "ui.field.feedback.message" => "Message",
                "ui.field.feedback.contact" => "Contact",
                "ui.text.feedback.helper" => "Include steps to reproduce.",
                "ui.button.back" => "Back",
                "ui.button.send" => "Send",
                "ui.button.server.add" => "Add signal",
                "ui.dialog.server.connecting" => "Connecting",
                "ui.message.server.connecting" => "Contacting server",
                "ui.panel.credits" => "Credits",
                "ui.text.credits.body" => "Credits body.",
                "ui.text.credits.thanks" => "Thank you.",
                _ => value,
            };
        }

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
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
