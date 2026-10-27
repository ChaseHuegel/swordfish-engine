using System.Collections.Generic;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

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
}
