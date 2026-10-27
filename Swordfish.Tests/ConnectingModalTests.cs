using System.Collections.Generic;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class ConnectingModalTests
{
    [Fact]
    public void ModalEqualityOperatorsWork()
    {
        Modal first = new("test.modal");
        Modal second = new("test.modal");
        Modal third = new("other.modal");

        Assert.True(first == second);
        Assert.False(first != second);
        Assert.False(first == third);
        Assert.True(first != third);
        Assert.True(first.Equals(second));
        Assert.False(first.Equals(third));
    }

    [Fact]
    public void ConnectingModalHasCorrectIdentifier()
    {
        var modal = new ConnectingModal(soundEffectService: null!, new StubLocalization());
        Assert.Equal("core.connecting", modal.ID.ID);
        Assert.Equal(ConnectingModal.Modal, modal.ID);
    }

    [Fact]
    public void OnExitFiresCancellationOnce()
    {
        var modal = new ConnectingModal(soundEffectService: null!, new StubLocalization());
        var cancelled = 0;

        modal.Create(onCancel: () => cancelled++);

        modal.OnExit();
        Assert.Equal(1, cancelled);

        modal.OnExit();
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public void CloseClearsCancellationBeforeExit()
    {
        var modal = new ConnectingModal(soundEffectService: null!, new StubLocalization());
        var cancelled = false;

        modal.Create(onCancel: () => cancelled = true);
        modal.Close(menu: null!);

        modal.OnExit();
        Assert.False(cancelled);
    }

    [Fact]
    public void RenderPageEmitsTargetAndScanningBeam()
    {
        var localization = new StubLocalization();
        var modal = new ConnectingModal(soundEffectService: null!, localization);
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), new UIController());

        modal.Create(target: "127.0.0.1:7777");

        // Advance UI clock so the traveling signal beam is inside the clipped track
        ui.Build(0.5f);

        using (ui.Element(id: "Blocker_Modal"))
        {
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };
            modal.RenderPage(delta: 0.5d, ui, menu: null!);
        }

        List<RenderCommand<Material>> commands = ui.Build(0f);

        Assert.Contains(commands, command => command.Text == "127.0.0.1:7777");
        Assert.Contains(commands, command => command.Color == UiTheme.Accent);
        Assert.Contains(commands, command => command.ID == "Button_CancelConnect");
    }

    private sealed class StubLocalization : ILocalization
    {
        public string? GetString(string value) => value;
        public string? GetString(string value, string cultureName) => value;
        public string? GetString(string value, System.Globalization.CultureInfo cultureInfo) => value;
    }

    private sealed class StubTextEngine : ITextEngine
    {
        private static readonly TextLayout _cachedLayout = new(
            new TextConstraints(32, 32, 32, 32),
            [],
            ["OK"],
            lineHeight: 20
        );

        public ITypeface GetDefaultTypeface() => null!;
        public ITypeface[] GetTypefaces() => [];
        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => new(32, 32, 32, 32);
        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => new(32, 32, 32, 32);
        public TextLayout Layout(FontOptions fontOptions, string text) => _cachedLayout;
        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => _cachedLayout;
        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => _cachedLayout;
        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => _cachedLayout;
        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => ["OK"];
        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => ["OK"];
    }
}
