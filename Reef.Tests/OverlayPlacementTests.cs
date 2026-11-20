using System.Collections.Generic;
using Reef.Constraints;
using Reef.UI;
using Xunit;

namespace Reef.Tests;

public class OverlayPlacementTests
{
    [Fact]
    public void ForElementAnchorsTopRightOnFirstFrameWithImplicitOverlay()
    {
        var ui = new UIBuilder<PixelTexture>(800, 600, textEngine: null!, new UIController());

        using (ui.Element("container"))
        {
            ui.Constraints = new UI.Constraints
            {
                X = new Fixed(100),
                Y = new Fixed(80),
                Width = new Fixed(60),
                Height = new Fixed(60),
            };
            ui.ClipConstraints = new UI.Constraints
            {
                Width = new Fixed(60),
                Height = new Fixed(60),
            };

            using (ui.Element("slot"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(48),
                    Height = new Fixed(48),
                };
            }

            // Declared inside a clipped container without setting ui.Overlay explicitly.
            using (ui.Element("tooltip"))
            {
                ui.OverlayPlacement = OverlayPlacement.ForElement("slot");
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(180),
                    Height = new Fixed(90),
                };
            }
        }

        // Declared after container to prove the overlay still paints last.
        using (ui.Element("sibling"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(40),
                Height = new Fixed(40),
            };
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0.016);
        RenderCommand<PixelTexture> slot = Find(commands, "slot");
        RenderCommand<PixelTexture> tooltip = Find(commands, "tooltip");

        Assert.Equal(slot.Rect.Right + 8, tooltip.Rect.Left);
        Assert.Equal(slot.Rect.Top, tooltip.Rect.Top);
        Assert.Equal(tooltip.Rect, tooltip.ClipRect);
        Assert.Equal("tooltip", commands[^1].ID);
    }

    [Fact]
    public void ForElementFlipsLeftWhenRightEdgeOverflowsViewport()
    {
        var ui = new UIBuilder<PixelTexture>(600, 400, textEngine: null!, new UIController());

        using (ui.Element("slot"))
        {
            ui.Constraints = new UI.Constraints
            {
                X = new Fixed(500),
                Y = new Fixed(100),
                Width = new Fixed(48),
                Height = new Fixed(48),
            };
        }

        using (ui.Element("tooltip"))
        {
            ui.OverlayPlacement = OverlayPlacement.ForElement("slot");
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(200),
                Height = new Fixed(80),
            };
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0.016);
        RenderCommand<PixelTexture> slot = Find(commands, "slot");
        RenderCommand<PixelTexture> tooltip = Find(commands, "tooltip");

        Assert.Equal(slot.Rect.Left - 200 - 8, tooltip.Rect.Left);
        Assert.Equal(slot.Rect.Top, tooltip.Rect.Top);
    }

    [Fact]
    public void ForElementFlipsVerticalAlignmentWhenBottomEdgeOverflowsViewport()
    {
        var ui = new UIBuilder<PixelTexture>(800, 400, textEngine: null!, new UIController());

        using (ui.Element("slot"))
        {
            ui.Constraints = new UI.Constraints
            {
                X = new Fixed(120),
                Y = new Fixed(330),
                Width = new Fixed(48),
                Height = new Fixed(48),
            };
        }

        using (ui.Element("tooltip"))
        {
            ui.OverlayPlacement = OverlayPlacement.ForElement("slot");
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(180),
                Height = new Fixed(120),
            };
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0.016);
        RenderCommand<PixelTexture> slot = Find(commands, "slot");
        RenderCommand<PixelTexture> tooltip = Find(commands, "tooltip");

        Assert.Equal(slot.Rect.Right + 8, tooltip.Rect.Left);
        Assert.Equal(slot.Rect.Bottom, tooltip.Rect.Bottom);
    }

    [Fact]
    public void ForElementFlipsBothAxesAtBottomRightCornerOnFirstFrame()
    {
        var ui = new UIBuilder<PixelTexture>(600, 400, textEngine: null!, new UIController());

        using (ui.Element("slot"))
        {
            ui.Constraints = new UI.Constraints
            {
                X = new Fixed(520),
                Y = new Fixed(330),
                Width = new Fixed(48),
                Height = new Fixed(48),
            };
        }

        // Fit-sized overlay whose size is computed in Pass 1 before Pass 2 placement.
        using (ui.Element("tooltip"))
        {
            ui.OverlayPlacement = OverlayPlacement.ForElement("slot");
            ui.Padding = new Padding(10);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fit(),
                Height = new Fit(),
            };

            using (ui.Element())
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(180),
                    Height = new Fixed(100),
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0.016);
        RenderCommand<PixelTexture> slot = Find(commands, "slot");
        RenderCommand<PixelTexture> tooltip = Find(commands, "tooltip");

        Assert.Equal(200, tooltip.Rect.Size.X);
        Assert.Equal(120, tooltip.Rect.Size.Y);
        Assert.Equal(slot.Rect.Left - 200 - 8, tooltip.Rect.Left);
        Assert.Equal(slot.Rect.Bottom, tooltip.Rect.Bottom);
    }

    [Fact]
    public void ResolveClampsInsideViewportMarginWhenNeitherSideFits()
    {
        // Left has more space (84px) than right (76px), so flip picks left and clamps to margin.
        var anchorRightHeavy = new IntRect(left: 100, top: 80, right: 148, bottom: 128);
        OverlayPlacement placement = OverlayPlacement.ForRect(anchorRightHeavy, gap: 8, viewportMargin: 8);

        IntVector2 leftClamped = OverlayPlacement.Resolve(
            anchorRightHeavy,
            overlaySize: new IntVector2(200, 150),
            viewportSize: new IntVector2(240, 180),
            in placement
        );

        Assert.Equal(8, leftClamped.X);
        Assert.Equal(22, leftClamped.Y);

        // Right has more space than left, so it stays on the right and clamps to max X.
        var anchorLeftHeavy = new IntRect(left: 20, top: 80, right: 68, bottom: 128);
        IntVector2 rightClamped = OverlayPlacement.Resolve(
            anchorLeftHeavy,
            overlaySize: new IntVector2(200, 150),
            viewportSize: new IntVector2(240, 180),
            in placement
        );

        Assert.Equal(32, rightClamped.X);
        Assert.Equal(22, rightClamped.Y);
    }

    [Fact]
    public void ResolveHandlesOverlayLargerThanViewportWithoutThrowing()
    {
        var anchor = new IntRect(left: 50, top: 50, right: 98, bottom: 98);
        OverlayPlacement placement = OverlayPlacement.ForRect(anchor, viewportMargin: 8);

        IntVector2 pos = OverlayPlacement.Resolve(
            anchor,
            overlaySize: new IntVector2(500, 400),
            viewportSize: new IntVector2(300, 200),
            in placement
        );

        Assert.Equal(8, pos.X);
        Assert.Equal(8, pos.Y);
    }

    [Fact]
    public void ResolveSupportsBottomTopLeftAndCenterAlignments()
    {
        var anchor = new IntRect(left: 200, top: 200, right: 260, bottom: 240);
        var size = new IntVector2(100, 50);
        var viewport = new IntVector2(800, 600);

        OverlayPlacement bottomCenter = OverlayPlacement.ForRect(anchor, side: OverlaySide.Bottom, align: OverlayAlign.Center, gap: 4);
        Assert.Equal(new IntVector2(180, 244), OverlayPlacement.Resolve(anchor, size, viewport, in bottomCenter));

        OverlayPlacement topEnd = OverlayPlacement.ForRect(anchor, side: OverlaySide.Top, align: OverlayAlign.End, gap: 6);
        Assert.Equal(new IntVector2(160, 144), OverlayPlacement.Resolve(anchor, size, viewport, in topEnd));

        OverlayPlacement leftCenter = OverlayPlacement.ForRect(anchor, side: OverlaySide.Left, align: OverlayAlign.Center, gap: 8);
        Assert.Equal(new IntVector2(92, 195), OverlayPlacement.Resolve(anchor, size, viewport, in leftCenter));
    }

    [Fact]
    public void AtPointUsesTwelvePixelDefaultGapAndFlipsNearEdges()
    {
        var ui = new UIBuilder<PixelTexture>(400, 300, textEngine: null!, new UIController());

        using (ui.Element("menu"))
        {
            ui.OverlayPlacement = OverlayPlacement.AtPoint(new IntVector2(350, 260));
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(120),
                Height = new Fixed(80),
            };
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0.016);
        RenderCommand<PixelTexture> menu = Find(commands, "menu");

        Assert.Equal(350 - 120 - 12, menu.Rect.Left);
        Assert.Equal(260 - 80, menu.Rect.Top);
    }

    [Fact]
    public void ScaledCanvasFlipsAgainstLogicalViewportBounds()
    {
        var ui = new UIBuilder<PixelTexture>(1200, 800, textEngine: null!, new UIController());
        ui.SetScale(configuredScale: 2f); // Logical 600 x 400

        using (ui.Element("slot"))
        {
            ui.Constraints = new UI.Constraints
            {
                X = new Fixed(500),
                Y = new Fixed(100),
                Width = new Fixed(48),
                Height = new Fixed(48),
            };
        }

        using (ui.Element("tooltip"))
        {
            ui.OverlayPlacement = OverlayPlacement.ForElement("slot");
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(180),
                Height = new Fixed(60),
            };
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(0.016);
        RenderCommand<PixelTexture> slot = Find(commands, "slot");
        RenderCommand<PixelTexture> tooltip = Find(commands, "tooltip");

        Assert.Equal(slot.Rect.Left - 180 - 8, tooltip.Rect.Left);
    }

    private static RenderCommand<PixelTexture> Find(List<RenderCommand<PixelTexture>> commands, string id)
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
