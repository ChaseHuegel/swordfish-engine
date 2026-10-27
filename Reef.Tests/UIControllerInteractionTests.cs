using System.Collections.Generic;
using System.Numerics;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

public class UIControllerInteractionTests
{
    [Fact]
    public void ClickingFocusedElementPreservesFocusDuringBuild()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        //  Frame 1: Hover over input
        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderInput(ui, out bool focusedFrame1);
        ui.Build(delta: 0f);
        Assert.False(focusedFrame1);

        //  Frame 2: Click on input to focus it
        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderInput(ui, out _);
        ui.Build(delta: 0.1f);
        Assert.True(ui.IsKeyboardCaptured);

        //  Frame 3: Release mouse
        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderInput(ui, out bool focusedFrame3);
        ui.Build(delta: 0.1f);
        Assert.True(focusedFrame3);

        //  Frame 4: Click again on the already-focused input — focus must remain true during build!
        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderInput(ui, out bool focusedDuringClickFrame);
        ui.Build(delta: 0.1f);
        Assert.True(focusedDuringClickFrame);

        //  Frame 5: Release mouse, then Frame 6: click outside to unfocus
        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderInput(ui, out _);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(90, 90, UIController.MouseButtons.Left);
        RenderInput(ui, out _);
        ui.Build(delta: 0.1f);
        Assert.False(ui.IsKeyboardCaptured);
    }

    [Fact]
    public void ActiveDragKeepsHeldStateWithoutBlockingHoverElsewhere()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        //  Frame 1: Idle at (10, 10) over slider
        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderSliderAndBackground(ui, out _, out _);
        ui.Build(delta: 0f);

        //  Frame 2: Press left mouse on slider at (10, 10)
        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderSliderAndBackground(ui, out _, out _);
        ui.Build(delta: 0.1f);

        //  Frame 3: Drag outside slider to (80, 80) while holding left mouse
        controller.UpdateMouse(80, 80, UIController.MouseButtons.Left);
        RenderSliderAndBackground(ui, out _, out _);
        ui.Build(delta: 0.1f);

        //  Frame 4: Read states resulting from Frame 3
        controller.UpdateMouse(80, 80, UIController.MouseButtons.Left);
        RenderSliderAndBackground(ui, out bool sliderHeld, out bool backgroundHovered);
        ui.Build(delta: 0.1f);

        Assert.True(sliderHeld);
        Assert.True(backgroundHovered);
    }

    [Fact]
    public void ActiveDragDoesNotConsumeHoverFromAnEarlierSibling()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        controller.UpdateMouse(50, 10, UIController.MouseButtons.None);
        RenderDragSourceAndTarget(ui, out _, out _);
        ui.Build(delta: 0f);

        controller.UpdateMouse(50, 10, UIController.MouseButtons.Left);
        RenderDragSourceAndTarget(ui, out _, out _);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderDragSourceAndTarget(ui, out _, out _);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.Left);
        RenderDragSourceAndTarget(ui, out bool sourceHeld, out bool targetHovered);
        ui.Build(delta: 0.1f);

        Assert.True(sourceHeld);
        Assert.True(targetHovered);
    }

    [Fact]
    public void PerElementRightAndMiddleClickInteractionsWork()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderRightClickTarget(ui, out _);
        ui.Build(delta: 0f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.Right);
        RenderRightClickTarget(ui, out _);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(10, 10, UIController.MouseButtons.None);
        RenderRightClickTarget(ui, out bool rightClicked);
        ui.Build(delta: 0.1f);

        Assert.True(rightClicked);
    }

    [Fact]
    public void ScrollWheelRoutesToTopMostHoveredScrollableElement()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        controller.UpdateMouse(20, 20, UIController.MouseButtons.None, scrollDeltaX: 0f, scrollDeltaY: -3f);
        RenderNestedScrollViews(ui, out _, out _);
        ui.Build(delta: 0f);

        RenderNestedScrollViews(ui, out float outerDelta, out float innerDelta);
        ui.Build(delta: 0.1f);

        Assert.Equal(0f, outerDelta);
        Assert.Equal(-3f, innerDelta);
    }

    [Fact]
    public void KeyHeldAndReleasedAndInputEqualityOperatorWork()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        var pressInput = new UIController.Input(UIController.Key.Backspace, pressed: true);
        var releaseInput = new UIController.Input(UIController.Key.Backspace, pressed: false);

        Assert.True(pressInput == UIController.Key.Backspace);
        Assert.False(releaseInput == UIController.Key.Backspace);

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Shift, pressed: true)]);
        Assert.True(ui.IsPressed(UIController.Key.Shift));
        Assert.True(ui.IsHeld(UIController.Key.Shift));

        //  Next frame: empty input buffer, but Shift has not been released so IsHeld remains true
        controller.UpdateInputBuffer([]);
        Assert.False(ui.IsPressed(UIController.Key.Shift));
        Assert.True(ui.IsHeld(UIController.Key.Shift));

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Shift, pressed: false)]);
        Assert.False(ui.IsHeld(UIController.Key.Shift));
        Assert.True(ui.IsReleased(UIController.Key.Shift));
    }

    [Fact]
    public void OverlayEscapesParentClipAndRendersAndHitTestsOnTop()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(100, 100, new StubTextEngine(), controller);

        controller.UpdateMouse(10, 40, UIController.MouseButtons.None);
        RenderOverlayOverSibling(ui, out _, out _);
        ui.Build(delta: 0f);

        controller.UpdateMouse(10, 40, UIController.MouseButtons.Left);
        RenderOverlayOverSibling(ui, out _, out _);
        ui.Build(delta: 0.1f);

        controller.UpdateMouse(10, 40, UIController.MouseButtons.None);
        RenderOverlayOverSibling(ui, out bool overlayClicked, out bool laterRootClicked);
        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0.1f);

        Assert.True(overlayClicked);
        Assert.False(laterRootClicked);
        Assert.Equal("dropdownOverlay", commands[^1].ID);
    }

    private static void RenderInput(UIBuilder<PixelTexture> ui, out bool focused)
    {
        using (ui.Element("input"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(50),
                Height = new Fixed(50),
            };
            focused = ui.Focused();
        }
    }

    private static void RenderSliderAndBackground(UIBuilder<PixelTexture> ui, out bool sliderHeld, out bool backgroundHovered)
    {
        using (ui.Element("background"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(100),
            };
            backgroundHovered = ui.Hovering();
        }

        using (ui.Element("slider"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(30),
                Height = new Fixed(30),
            };
            sliderHeld = ui.Held();
        }
    }

    private static void RenderDragSourceAndTarget(
        UIBuilder<PixelTexture> ui,
        out bool sourceHeld,
        out bool targetHovered
    )
    {
        using (ui.Element("target"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(30),
                Height = new Fixed(30),
            };
            targetHovered = ui.Hovering();
        }

        using (ui.Element("source"))
        {
            ui.Constraints = new UI.Constraints
            {
                X = new Fixed(40),
                Width = new Fixed(30),
                Height = new Fixed(30),
            };
            sourceHeld = ui.Held();
        }
    }

    private static void RenderRightClickTarget(UIBuilder<PixelTexture> ui, out bool rightClicked)
    {
        using (ui.Element("slot"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(50),
                Height = new Fixed(50),
            };
            rightClicked = ui.Clicked(UIController.MouseButtons.Right);
        }
    }

    private static void RenderNestedScrollViews(UIBuilder<PixelTexture> ui, out float outerDelta, out float innerDelta)
    {
        using (ui.Element("outer"))
        {
            ui.VerticalScroll = true;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(100),
            };
            outerDelta = ui.GetElementScrollDelta();

            using (ui.Element("inner"))
            {
                ui.VerticalScroll = true;
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(50),
                    Height = new Fixed(50),
                };
                innerDelta = ui.GetElementScrollDelta();
            }
        }
    }

    private static void RenderOverlayOverSibling(UIBuilder<PixelTexture> ui, out bool overlayClicked, out bool laterRootClicked)
    {
        using (ui.Element("clippedParent"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(20),
            };
            ui.ClipConstraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(20),
            };

            using (ui.Element("dropdownOverlay"))
            {
                ui.Overlay = true;
                ui.Color = new Vector4(1f);
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(50),
                    Height = new Fixed(60),
                };
                overlayClicked = ui.Clicked();
            }
        }

        using (ui.Element("laterRoot"))
        {
            ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 1f);
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(100),
            };
            laterRootClicked = ui.Clicked();
        }
    }

    private sealed class StubTextEngine : ITextEngine
    {
        public ITypeface GetDefaultTypeface() => null!;

        public ITypeface[] GetTypefaces() => [];

        public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
        {
            typeface = null;
            return false;
        }

        public TextConstraints Measure(FontOptions fontOptions, string text) => default;

        public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => default;

        public TextLayout Layout(FontOptions fontOptions, string text) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length) => TextLayout.Empty;

        public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth) => TextLayout.Empty;

        public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [];

        public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth) => [];
    }
}
