using System;
using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace Swordfish.Tests;

public class ScrollInputReproTests
{
    [Fact]
    public void PlainElementInsideScrollAreaReceivesClicks()
    {
        AssertClicks(new Harness(shell: false, scrollArea: true, drawChoice: false), "repro_click");
    }

    [Fact]
    public void ChoiceWidgetInsideScrollAreaReceivesClicks()
    {
        AssertClicks(new Harness(shell: false, scrollArea: true, drawChoice: true), "repro_choice.1");
    }

    [Fact]
    public void ChoiceWidgetInsideShellAndScrollAreaReceivesClicks()
    {
        AssertClicks(new Harness(shell: true, scrollArea: true, drawChoice: true), "repro_choice.1");
    }

    [Fact]
    public void ChoiceWidgetOutsideScrollAreaReceivesClicks()
    {
        AssertClicks(new Harness(shell: false, scrollArea: false, drawChoice: true), "repro_choice.1");
    }

    private static void AssertClicks(Harness harness, string targetId)
    {
        harness.Render();
        IntRect target = harness.Find(targetId);
        int x = target.Left + target.Size.X / 2;
        int y = target.Top + target.Size.Y / 2;

        harness.Frame(x, y, UIController.MouseButtons.None, 0.05f);
        harness.Frame(x, y, UIController.MouseButtons.Left, 0.1f);
        harness.Frame(x, y, UIController.MouseButtons.None, 0.2f);

        Assert.True(
            harness.PlainHits + harness.ChoiceHits > 0,
            $"No click reached \"{targetId}\" at {x},{y}."
        );
    }

    private sealed class Harness(bool shell, bool scrollArea, bool drawChoice)
    {
        private readonly UIController _controller = new();
        private readonly ScrollAreaState _scroll = new();
        private UIBuilder<Material> _ui = null!;

        public int PlainHits { get; private set; }

        public int ChoiceHits { get; private set; }

        public IntRect Find(string id)
        {
            List<RenderCommand<Material>> commands = _ui.Build(0f);
            for (var i = 0; i < commands.Count; i++)
            {
                if (commands[i].ID == id)
                {
                    return commands[i].Rect;
                }
            }

            throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
        }

        public void Frame(int x, int y, UIController.MouseButtons buttons, float delta)
        {
            _controller.UpdateMouse(x, y, buttons);
            Render();
            _ui.Build(delta);
        }

        public void Render()
        {
            _ui ??= new UIBuilder<Material>(400, 260, new StubTextEngine(), _controller);

            if (!shell)
            {
                if (!scrollArea)
                {
                    using (_ui.Element())
                    {
                        _ui.Constraints = new Constraints { Width = new Fixed(400), Height = new Fixed(260) };
                        DrawBody(_ui);
                    }

                    return;
                }

                DrawScrollOrBody(_ui);
                return;
            }

            using (_ui.Element())
            {
                _ui.LayoutDirection = LayoutDirection.Vertical;
                _ui.Padding = new Padding(0, 12, 0, 0);
                _ui.Spacing = 12;
                _ui.Constraints = new Constraints
                {
                    Width = new Fixed(360),
                    Height = new Fixed(220),
                    Anchors = Anchors.Center,
                };
                _ui.ClipConstraints = new Constraints { Width = new Relative(1f), Height = new Relative(1f) };

                using (_ui.Element())
                {
                    _ui.LayoutDirection = LayoutDirection.None;
                    _ui.Padding = new Padding(2);
                    _ui.Constraints = new Constraints { Width = new Fill(), Height = new Fill() };

                    using (_ui.Element())
                    {
                        _ui.LayoutDirection = LayoutDirection.Vertical;
                        _ui.Padding = new Padding(24);
                        _ui.Constraints = new Constraints { Width = new Fill(), Height = new Fill() };
                        _ui.ClipConstraints = new Constraints { Width = new Relative(1f), Height = new Relative(1f) };
                        DrawScrollOrBody(_ui);
                    }
                }
            }
        }

        private void DrawScrollOrBody(UIBuilder<Material> ui)
        {
            if (!scrollArea)
            {
                DrawBody(ui);
                return;
            }

            new ScrollArea().Render(
                ui,
                context: _scroll,
                callerConstraints: new ConstraintsOverride
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                content_default: DrawBody
            );
        }

        private void DrawBody(UIBuilder<Material> ui)
        {
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints { Width = new Fill(), Height = new Fit() };

                using (ui.Element("repro_click"))
                {
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(200),
                        Height = new Fixed(30),
                        X = new Fixed(10),
                        Y = new Fixed(10),
                    };

                    if (ui.Clicked())
                    {
                        PlainHits++;
                    }
                }

                if (!drawChoice)
                {
                    return;
                }

                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(240),
                        Height = new Fixed(32),
                        X = new Fixed(10),
                        Y = new Fixed(60),
                    };

                    ControlResult<string> result = Widgets.SegmentedControl(
                        ui,
                        "repro_choice",
                        new Constraints { Width = new Fixed(240), Height = new Fixed(32) },
                        ["Off", "On"],
                        "Off",
                        option => option,
                        enabled: true
                    );

                    if (result.Changed)
                    {
                        ChoiceHits++;
                    }
                }
            }
        }
    }
}

/// <summary>Repro of scroll input using generated markup.</summary>
public class ScrollInputMarkupReproTests
{
    [Fact]
    public void MarkupChoiceInsideScrolledPaneReceivesClicks()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(500, 300, new StubTextEngine(), controller);
        var view = new ScrollReproView(new TestLocalization());
        var changes = 0;
        view.ChoiceChanged = _ => changes++;

        List<RenderCommand<Material>> Frame(int x, int y, UIController.MouseButtons buttons, float delta)
        {
            controller.UpdateMouse(x, y, buttons);
            view.Render(ui, new ScrollReproState(ShowChoice: true, Index: 0));
            return ui.Build(delta);
        }

        IntRect cell = Find(Frame(0, 0, UIController.MouseButtons.None, 0f), "repro_choice.1").Rect;
        int cx = cell.Left + cell.Size.X / 2;
        int cy = cell.Top + cell.Size.Y / 2;
        Frame(cx, cy, UIController.MouseButtons.None, 0.05f);
        Frame(cx, cy, UIController.MouseButtons.Left, 0.1f);
        Frame(cx, cy, UIController.MouseButtons.None, 0.2f);

        Assert.True(changes > 0, $"No click reached the markup choice at {cx},{cy}.");
    }

    [Fact]
    public void MarkupPlainElementInsideScrolledPaneReceivesClicks()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(500, 300, new StubTextEngine(), controller);
        var view = new ScrollReproView(new TestLocalization());
        var clicks = 0;
        view.PlainClicked = () => clicks++;

        List<RenderCommand<Material>> Frame(int x, int y, UIController.MouseButtons buttons, float delta)
        {
            controller.UpdateMouse(x, y, buttons);
            view.Render(ui, new ScrollReproState(ShowChoice: false, Index: 0));
            return ui.Build(delta);
        }

        IntRect target = Find(Frame(0, 0, UIController.MouseButtons.None, 0f), "repro_click").Rect;
        int cx = target.Left + target.Size.X / 2;
        int cy = target.Top + target.Size.Y / 2;
        Frame(cx, cy, UIController.MouseButtons.None, 0.05f);
        Frame(cx, cy, UIController.MouseButtons.Left, 0.1f);
        Frame(cx, cy, UIController.MouseButtons.None, 0.2f);

        Assert.True(clicks > 0, $"No click reached the markup element at {cx},{cy}.");
    }

    private static RenderCommand<Material> Find(List<RenderCommand<Material>> commands, string id)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].ID == id)
            {
                return commands[i];
            }
        }

        throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
    }
}
