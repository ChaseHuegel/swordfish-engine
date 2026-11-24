using System;
using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace WaywardBeyond.Tests;

public class StepperComponentTests
{
    [Fact]
    public void StepperStatesMatchSnapshots()
    {
        Snapshot("component-stepper-enabled", new StepperParameters("human", Enabled: true));
        Snapshot("component-stepper-disabled", new StepperParameters("human", Enabled: false));
        Snapshot("component-stepper-min", new StepperParameters("human", Enabled: true, CanDecrease: false));
        Snapshot("component-stepper-max", new StepperParameters("human", Enabled: true, CanIncrease: false));
    }

    [Fact]
    public void LongValueClipsBetweenTheArrows()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(300, 60, new StubTextEngine(), controller);
        var parameters = new StepperParameters("Meteor Lake-P HD Audio Controller HDMI / DisplayPort 3 Output", Enabled: true);

        Render(ui, parameters, controller, 20, 20, UIController.MouseButtons.None);
        List<RenderCommand<Material>> commands = ui.Build(0.1f);

        RenderCommand<Material> previous = commands.Find(command => command.Text == "<");
        RenderCommand<Material> next = commands.Find(command => command.Text == ">");
        RenderCommand<Material> value = commands.Find(command => command.Text == parameters.Value);

        Assert.Equal("<", previous.Text);
        Assert.Equal(">", next.Text);
        Assert.Equal(parameters.Value, value.Text);
        Assert.True(value.ClipRect.Left >= previous.Rect.Left + previous.Rect.Size.X);
        Assert.True(value.ClipRect.Left + value.ClipRect.Size.X <= next.Rect.Left);
    }

    [Fact]
    public void HoveredArrowUsesTheHighlightColors()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(300, 60, new StubTextEngine(), controller);
        var parameters = new StepperParameters("human", Enabled: true);

        Render(ui, parameters, controller, 20, 20, UIController.MouseButtons.None);
        ui.Build(0f);
        Render(ui, parameters, controller, 20, 20, UIController.MouseButtons.None);
        System.Collections.Generic.List<RenderCommand<Material>> commands = ui.Build(0.1f);

        RenderCommand<Material> glyph = Assert.Single(commands, command => command.Text == "<");
        Assert.Equal(0.6471f, glyph.Color.X, 3);

        List<RenderCommand<Material>> arrows = commands.FindAll(
            command => command.Rect.Size.X == 32 && command.Rect.Size.Y == 30
        );

        //  --wb-button-hover is #1b2538, applied only to the hovered arrow.
        const float hoverFillRed = 27f / 255f;
        Assert.Equal(2, arrows.Count);
        Assert.Single(arrows, arrow => Math.Abs(arrow.Color.X - hoverFillRed) < 0.0005f);
    }

    [Fact]
    public void BothArrowsFireBetweenTheBounds()
    {
        var steps = 0;
        var parameters = new StepperParameters(
            "human",
            Enabled: true,
            CanDecrease: true,
            CanIncrease: true,
            Id: "step",
            OnStep: (_, delta) => steps += delta
        );

        Click(parameters, 10, 16);
        Assert.Equal(-1, steps);

        Click(parameters, 206, 16);
        Assert.Equal(0, steps);
    }

    [Fact]
    public void DecreaseArrowIsInertAtTheMinimum()
    {
        var steps = 0;
        var parameters = new StepperParameters(
            "human",
            Enabled: true,
            CanDecrease: false,
            CanIncrease: true,
            Id: "step",
            OnStep: (_, delta) => steps += delta
        );

        Click(parameters, 10, 16);
        Assert.Equal(0, steps);

        Click(parameters, 206, 16);
        Assert.Equal(1, steps);
    }

    [Fact]
    public void IncreaseArrowIsInertAtTheMaximum()
    {
        var steps = 0;
        var parameters = new StepperParameters(
            "human",
            Enabled: true,
            CanDecrease: true,
            CanIncrease: false,
            Id: "step",
            OnStep: (_, delta) => steps += delta
        );

        Click(parameters, 206, 16);
        Assert.Equal(0, steps);

        Click(parameters, 10, 16);
        Assert.Equal(-1, steps);
    }

    [Fact]
    public void DisabledStepperIgnoresClicks()
    {
        var steps = 0;
        var parameters = new StepperParameters(
            "human",
            Enabled: false,
            CanDecrease: true,
            CanIncrease: true,
            Id: "step",
            OnStep: (_, delta) => steps += delta
        );

        Click(parameters, 10, 16);
        Click(parameters, 206, 16);

        Assert.Equal(0, steps);
    }

    private static void Click(StepperParameters parameters, int x, int y)
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(300, 60, new StubTextEngine(), controller);

        Render(ui, parameters, controller, x, y, UIController.MouseButtons.None);
        ui.Build(0f);
        Render(ui, parameters, controller, x, y, UIController.MouseButtons.Left);
        ui.Build(0.1f);
        Render(ui, parameters, controller, x, y, UIController.MouseButtons.None);
        ui.Build(0.2f);
    }

    private static void Render(
        UIBuilder<Material> ui,
        StepperParameters parameters,
        UIController controller,
        int x,
        int y,
        UIController.MouseButtons buttons
    )
    {
        controller.UpdateMouse(x, y, buttons);
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                X = new Fixed(0),
                Y = new Fixed(0),
                Width = new Fixed(216),
                Height = new Fixed(32),
            };
            new Stepper().Render(
                ui,
                parameters,
                callerConstraints: new ConstraintsOverride
                {
                    Width = new Fixed(216),
                    Height = new Fixed(32),
                }
            );
        }
    }

    private static void Snapshot(string name, StepperParameters parameters)
    {
        UiSnapshotTests.Compare(name, 240, 40, ui =>
        {
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    X = new Fixed(12),
                    Y = new Fixed(4),
                    Width = new Fixed(216),
                    Height = new Fixed(32),
                };
                new Stepper().Render(
                    ui,
                    parameters,
                    callerConstraints: new ConstraintsOverride
                    {
                        Width = new Fixed(216),
                        Height = new Fixed(32),
                    }
                );
            }
        });
    }
}
