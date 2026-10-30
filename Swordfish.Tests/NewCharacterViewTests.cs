using System.Collections.Generic;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class NewCharacterViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    [Fact]
    public void ColonistFormMatchesSnapshot()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.Compare($"{width}x{height}-new-character", width, height, ui =>
            {
                var view = new NewCharacterView(new TestLocalization()) { Input = new FakeInputService() };
                view.Render(ui, CreateState(view));
            });
        }
    }

    /// <summary>Builds the default form state: every stat at one with all spacer points unspent.</summary>
    private static NewCharacterState CreateState(NewCharacterView view)
    {
        string[] labels =
        [
            "ui.label.character.strength",
            "ui.label.character.precision",
            "ui.label.character.awareness",
            "ui.label.character.charisma",
            "ui.label.character.education",
            "ui.label.character.resolve",
        ];

        List<CharacterStatRow> stats = [];
        for (var i = 0; i < labels.Length; i++)
        {
            stats.Add(new CharacterStatRow(labels[i], "1", i == 0, false, true));
        }

        return new NewCharacterState(
            Tab: 0,
            SpacerPoints: 18,
            Appearance: new Material(shader: null!),
            new SelectorState("ui.field.character.species", "human", Enabled: false, CanDecrease: false, CanIncrease: false),
            new SelectorState("ui.field.character.gender", "body.gender.neutral", Enabled: false, CanDecrease: false, CanIncrease: false),
            stats,
            "ui.label.info",
            view.InfoText("ui.label.character.strength", "ui.text.character.strength"),
            "ui.message.character.unspentPoints",
            StatusDanger: false,
            StatusWarning: true,
            CanCreate: true
        );
    }
}
