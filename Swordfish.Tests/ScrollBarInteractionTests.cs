using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using Xunit;

namespace Swordfish.Tests;

public class ScrollBarInteractionTests
{
    private const int ViewWidth = 400;
    private const int ViewHeight = 300;
    private const int ScrollBarWidth = 6;
    private const int ScrollBarX = ViewWidth - ScrollBarWidth / 2;

    [Fact]
    public void DraggingTheThumbScrollsWhileTheCursorLeavesTheBar()
    {
        var harness = new Harness();
        harness.Scroll.SetMetrics(viewportHeight: ViewHeight, contentHeight: 1000);

        harness.Press(ScrollBarX, 70);
        harness.DragTo(100, 180);

        Assert.InRange(harness.Scroll.Offset, 360, 375);
    }

    [Fact]
    public void ClickingTheTrackJumpsTowardTheClick()
    {
        var harness = new Harness();
        harness.Scroll.SetMetrics(viewportHeight: ViewHeight, contentHeight: 1000);

        harness.Press(ScrollBarX, 250);

        Assert.InRange(harness.Scroll.Offset, 675, 690);
    }

    [Fact]
    public void ReleasingTheMouseStopsTheDrag()
    {
        var harness = new Harness();
        harness.Scroll.SetMetrics(viewportHeight: ViewHeight, contentHeight: 1000);

        harness.Press(ScrollBarX, 45);
        harness.DragTo(ScrollBarX, 180);
        int draggedOffset = harness.Scroll.Offset;

        harness.Release(ScrollBarX, 180);
        harness.Frame(ScrollBarX, 0, UIController.MouseButtons.None);
        harness.Frame(ScrollBarX, 0, UIController.MouseButtons.None);

        Assert.Equal(draggedOffset, harness.Scroll.Offset);
    }

    [Fact]
    public void TheThumbStaysInsideTheTrackAtBothEnds()
    {
        var harness = new Harness();
        harness.Scroll.SetMetrics(viewportHeight: ViewHeight, contentHeight: 1000);

        harness.Press(ScrollBarX, 45);
        harness.DragTo(ScrollBarX, 1000);
        Assert.Equal(700, harness.Scroll.Offset);
        Assert.InRange(harness.Scroll.Bar.ThumbOffset + harness.Scroll.Bar.ThumbRatio, 0.999f, 1.001f);

        harness.DragTo(ScrollBarX, -200);
        Assert.Equal(0, harness.Scroll.Offset);
        Assert.InRange(harness.Scroll.Bar.ThumbOffset, 0f, 0.001f);
    }

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;

        public Harness()
        {
            _ui = new UIBuilder<Material>(ViewWidth, ViewHeight, new StubTextEngine(), _controller);
        }

        public ScrollAreaState Scroll { get; } = new();

        public void Press(int x, int y)
        {
            Frame(x, y, UIController.MouseButtons.None);
            Frame(x, y, UIController.MouseButtons.Left);
            Frame(x, y, UIController.MouseButtons.Left);
        }

        public void DragTo(int x, int y)
        {
            Frame(x, y, UIController.MouseButtons.Left);
            Frame(x, y, UIController.MouseButtons.Left);
        }

        public void Release(int x, int y)
        {
            Frame(x, y, UIController.MouseButtons.None);
            Frame(x, y, UIController.MouseButtons.None);
        }

        public void Frame(int x, int y, UIController.MouseButtons buttons)
        {
            _controller.UpdateMouse(x, y, buttons);
            new ScrollArea().Render(
                _ui,
                context: Scroll,
                callerConstraints: new ConstraintsOverride
                {
                    Width = new Fixed(ViewWidth),
                    Height = new Fixed(ViewHeight),
                },
                content_default: DrawContent,
                idPrefix: "test_scroll"
            );
            _ui.Build(0.1d);
        }

        private static void DrawContent(UIBuilder<Material> ui)
        {
            using (ui.Element("test_scrollContent"))
            {
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints { Width = new Fill(), Height = new Fit() };

                for (var i = 0; i < 10; i++)
                {
                    using (ui.Element())
                    {
                        ui.Constraints = new Constraints { Width = new Fill(), Height = new Fixed(40) };
                    }
                }
            }
        }
    }
}
