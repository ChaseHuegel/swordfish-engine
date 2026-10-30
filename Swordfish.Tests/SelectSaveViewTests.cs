using System.Collections.Generic;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class SelectSaveViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    [Fact]
    public void GalaxyArchivePageMatchesLegacy()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.AssertPageMatchesLegacy($"{width}x{height}-select-save", width, height, ui =>
            {
                var scroll = new ScrollAreaState();
                var view = new SelectSaveView(new TestLocalization(), scroll);
                view.Render(ui, CreateState(view));
            });
        }
    }

    /// <summary>Builds the state that the legacy capture used: two archives with the first active.</summary>
    private static SelectSaveState CreateState(SelectSaveView view)
    {
        var rows = new List<SaveRowState>
        {
            new(
                "save-a",
                "Frontier Base",
                "CREATIVE",
                true,
                "ui.label.lastPlayed: ui.label.never",
                true
            ),
            new(
                "save-b",
                "Ashfall",
                "ADVENTURE",
                false,
                "ui.label.lastPlayed: ui.label.never",
                false
            ),
        };

        return new SelectSaveState(
            Remote: false,
            CanCreateSave: true,
            Tab: 0,
            HasActive: true,
            rows,
            new SaveDetailState(
                "Frontier Base",
                "Creative",
                "adjective42 subject0 verb0 destination0",
                "1 ui.word.hours",
                view.LastPlayedText(1_700_000_000_000)
            )
        );
    }
}
