using System.Collections.Generic;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class SelectCharacterViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    [Fact]
    public void CrewManifestPageMatchesLegacy()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.AssertPageMatchesLegacy($"{width}x{height}-select-character", width, height, ui =>
            {
                var view = new SelectCharacterView(new TestLocalization(), new ScrollAreaState());
                view.Render(ui, CreateState(view, height));
            });
        }
    }

    /// <summary>Builds the state that the legacy capture used: two colonists with the first active.</summary>
    private static SelectCharacterState CreateState(SelectCharacterView view, int height)
    {
        var portrait = new Material(shader: null!);
        var rows = new List<CharacterRowState>
        {
            new("1", "Ada", true, portrait),
            new("2", "Bram", false, portrait),
        };

        List<StatRowState> stats =
        [
            new("ui.label.character.strength", "4"),
            new("ui.label.character.precision", "3"),
            new("ui.label.character.awareness", "2"),
            new("ui.label.character.charisma", "1"),
            new("ui.label.character.education", "5"),
            new("ui.label.character.resolve", "6"),
        ];

        return new SelectCharacterState(
            HasCharacters: true,
            HasActive: true,
            CanLaunch: true,
            ShowAssignment: height >= 720,
            Tab: 0,
            rows,
            new CharacterDetailState(
                "Ada",
                view.LevelLine(0, "human"),
                view.Labeled("ui.label.lastPlayed", view.LastPlayedText(1_700_000_000_000)),
                view.Labeled("ui.label.timePlayed", view.TimePlayedText(3_600_000)),
                portrait,
                stats,
                "Frontier Base"
            )
        );
    }
}
