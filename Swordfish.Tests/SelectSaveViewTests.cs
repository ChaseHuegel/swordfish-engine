using System.Collections.Generic;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class SelectSaveViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    /// <summary>The star chart needs a measured field, so the capture renders two frames.</summary>
    [Fact]
    public void GalaxyArchivePageMatchesSnapshots()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            var scroll = new ScrollAreaState();
            var view = new SelectSaveView(new TestLocalization(), scroll);
            SelectSaveState state = CreateState(view);

            UiSnapshotTests.Compare(
                $"page-select-save-{width}x{height}",
                width,
                height,
                ui => view.Render(ui, state),
                frames: 2
            );
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
                GameMode.Creative,
                true,
                0,
                true,
                1_234_567_890L,
                3_600_000L,
                "adjective42 subject0 verb0 destination0"
            ),
            new(
                "save-b",
                "Ashfall",
                GameMode.Adventure,
                false,
                0,
                false,
                987_654_321L,
                120_000L,
                "obsidian apple ignites hyperion"
            ),
        };

        return new SelectSaveState(
            Remote: false,
            CanCreateSave: true,
            Tab: 0,
            HasActive: true,
            rows
        );
    }
}
