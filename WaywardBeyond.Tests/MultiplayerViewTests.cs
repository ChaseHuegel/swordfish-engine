using System.Collections.Generic;
using Swordfish.Tests;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace WaywardBeyond.Tests;

public class MultiplayerViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    [Fact]
    public void EmptyFavoritesMatchesLegacy()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.AssertPageMatchesLegacy($"{width}x{height}-multiplayer", width, height, ui =>
            {
                var view = CreateView();
                view.Render(ui, new MultiplayerState(
                    Remote: false,
                    BannerName: string.Empty,
                    BannerHost: string.Empty,
                    BannerPort: 0,
                    Favorites: true,
                    Scanning: true,
                    DiscoverKey: string.Empty,
                    Rows: [],
                    ErrorKey: string.Empty,
                    JoinEnabled: false
                ));
            });
        }
    }

    [Fact]
    public void SavedServersMatchLegacy()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.AssertPageMatchesLegacy($"{width}x{height}-multiplayer-rows", width, height, ui =>
            {
                var view = CreateView();
                view.Render(ui, new MultiplayerState(
                    Remote: false,
                    BannerName: string.Empty,
                    BannerHost: string.Empty,
                    BannerPort: 0,
                    Favorites: true,
                    Scanning: true,
                    DiscoverKey: string.Empty,
                    Rows:
                    [
                        new ServerRowState("Frontier Base", "192.168.1.20", 7777, "-", Selected: true, Favorite: false),
                        new ServerRowState("Ashfall", "10.0.0.4", 7778, "-", Selected: false, Favorite: false),
                        new ServerRowState("Drift Station", "example.test", 27015, "-", Selected: false, Favorite: false),
                    ],
                    ErrorKey: string.Empty,
                    JoinEnabled: true
                ));
            });
        }
    }

    private static MultiplayerView CreateView()
    {
        var view = new MultiplayerView(new TestLocalization(), new ScrollAreaState())
        {
            Input = new FakeInputService(),
        };
        view.HostState.Text.Clear().Append("127.0.0.1");
        view.PortState.Text.Clear().Append("7777");
        return view;
    }

    [Fact]
    public void PingBandsResolveExpectedGlyphToneAndText()
    {
        (int? Ping, IconGlyph Glyph, IconTone Tone, string Text, bool Good, bool Fair, bool Poor, bool Bad)[] cases =
        [
            (null, IconGlyph.Signal, IconTone.Dim, "-", false, false, false, false),
            (0, IconGlyph.Signal, IconTone.Success, "<1 ms", true, false, false, false),
            (59, IconGlyph.Signal, IconTone.Success, "59 ms", true, false, false, false),
            (60, IconGlyph.Signal, IconTone.Warning, "60 ms", false, true, false, false),
            (99, IconGlyph.Signal, IconTone.Warning, "99 ms", false, true, false, false),
            (100, IconGlyph.Signal, IconTone.Orange, "100 ms", false, false, true, false),
            (149, IconGlyph.Signal, IconTone.Orange, "149 ms", false, false, true, false),
            (150, IconGlyph.Signal, IconTone.Danger, "150 ms", false, false, false, true),
            (249, IconGlyph.Signal, IconTone.Danger, "249 ms", false, false, false, true),
            (250, IconGlyph.XMark, IconTone.Danger, "250 ms", false, false, false, true),
            (400, IconGlyph.XMark, IconTone.Danger, "400 ms", false, false, false, true),
        ];

        for (var i = 0; i < cases.Length; i++)
        {
            var c = cases[i];
            Assert.Equal(c.Glyph, MultiplayerView.PingGlyph(c.Ping));
            Assert.Equal(c.Tone, MultiplayerView.PingTone(c.Ping));
            Assert.Equal(c.Text, MultiplayerView.PingText(c.Ping));
            Assert.Equal(c.Good, MultiplayerView.IsPingGood(c.Ping));
            Assert.Equal(c.Fair, MultiplayerView.IsPingFair(c.Ping));
            Assert.Equal(c.Poor, MultiplayerView.IsPingPoor(c.Ping));
            Assert.Equal(c.Bad, MultiplayerView.IsPingBad(c.Ping));
        }
    }
}
