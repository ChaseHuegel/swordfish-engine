using System.Collections.Generic;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class NewCharacterStepperTests
{
    [Fact]
    public void SpeciesStepperRaisesTheSpeciesAction()
    {
        var harness = new Harness();

        harness.Click("species.next");

        Assert.Equal(1, harness.SpeciesDelta);
        Assert.Null(harness.GenderDelta);
    }

    [Fact]
    public void GenderStepperRaisesTheGenderAction()
    {
        var harness = new Harness();

        harness.Click("gender.previous");

        Assert.Equal(-1, harness.GenderDelta);
        Assert.Null(harness.SpeciesDelta);
    }

    [Fact]
    public void HoveringStatRowRaisesTheFocusAction()
    {
        var harness = new Harness();

        harness.Hover("StatRow_3");

        Assert.Equal(3, harness.HoveredStat);
    }

    private static NewCharacterState CreateState()
    {
        List<CharacterStatRow> stats =
        [
            new("ui.label.character.strength", "1", true, false, true),
            new("ui.label.character.precision", "1", false, false, true),
            new("ui.label.character.awareness", "1", false, false, true),
            new("ui.label.character.charisma", "1", false, false, true),
            new("ui.label.character.education", "1", false, false, true),
            new("ui.label.character.resolve", "1", false, false, true),
        ];

        return new NewCharacterState(
            Tab: 0,
            SpacerPoints: 18,
            Appearance: new Material(shader: null!),
            Species: new SelectorState("human", Enabled: true, CanDecrease: true, CanIncrease: true),
            Gender: new SelectorState("body.gender.neutral", Enabled: true, CanDecrease: true, CanIncrease: true),
            Stats: stats,
            InfoStatLabelKey: "ui.label.character.strength",
            InfoStatKey: "ui.text.character.strength",
            StatusKey: "ui.message.character.unspentPoints",
            StatusValue: "18",
            StatusDanger: false,
            StatusWarning: true,
            CanCreate: true
        );
    }

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly NewCharacterView _view;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;

        public Harness()
        {
            _view = new NewCharacterView(new TestLocalization()) { Input = new FakeInputService() };
            _view.SpeciesStepRequested = delta => SpeciesDelta = delta;
            _view.GenderStepRequested = delta => GenderDelta = delta;
            _view.StatHoverRequested = index => HoveredStat = index;
            _ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), _controller);
            Frame();
        }

        public int? SpeciesDelta { get; private set; }

        public int? GenderDelta { get; private set; }

        public int? HoveredStat { get; private set; }

        public void Click(string id)
        {
            IntRect target = Find(id);
            int x = target.Left + target.Size.X / 2;
            int y = target.Top + target.Size.Y / 2;

            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
        }

        public void Hover(string id)
        {
            IntRect target = Find(id);
            int x = target.Left + target.Size.X / 2;
            int y = target.Top + target.Size.Y / 2;

            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
            Frame();
        }

        private void Frame()
        {
            _delta += 0.05d;
            _view.Render(_ui, CreateState());
            _last = _ui.Build(_delta);
        }

        private IntRect Find(string id)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].ID == id)
                {
                    return _last[i].Rect;
                }
            }

            throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
        }
    }
}
