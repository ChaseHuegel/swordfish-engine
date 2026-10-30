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
                    BannerServer: string.Empty,
                    Favorites: true,
                    Scanning: true,
                    EmptyMessage: "ui.label.none",
                    EmptyFavorites: true,
                    Rows: [],
                    Error: null,
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
                    BannerServer: string.Empty,
                    Favorites: true,
                    Scanning: true,
                    EmptyMessage: "ui.label.none",
                    EmptyFavorites: true,
                    Rows:
                    [
                        new ServerRowState("Frontier Base", "192.168.1.20:7777", "-", Selected: true, Favorite: false),
                        new ServerRowState("Ashfall", "10.0.0.4:7778", "-", Selected: false, Favorite: false),
                        new ServerRowState("Drift Station", "example.test:27015", "-", Selected: false, Favorite: false),
                    ],
                    Error: null,
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
