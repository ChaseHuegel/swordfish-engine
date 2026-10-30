using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace Swordfish.Tests;

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
    public void BothArrowsFireBetweenTheBounds()
    {
        var steps = 0;
        var parameters = new StepperParameters(
            "human",
            Enabled: true,
            CanDecrease: true,
            CanIncrease: true,
            OnDecrease: () => steps--,
            OnIncrease: () => steps++
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
            OnDecrease: () => steps--,
            OnIncrease: () => steps++
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
            OnDecrease: () => steps--,
            OnIncrease: () => steps++
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
            OnDecrease: () => steps--,
            OnIncrease: () => steps++
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
