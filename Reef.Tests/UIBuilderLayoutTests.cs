using System.Collections.Generic;
using Reef.Constraints;
using Reef.MSDF;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

[Collection("TextEngine")]
public class UIBuilderLayoutTests
{
    [Fact]
    public void DirectChildOfRootResolvesRelativeWidthAndHeight()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(200, 200);

        using (ui.Element("root"))
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(200),
                Height = new Fixed(200),
            };

            using (ui.Element("child"))
            {
                ui.Constraints = new UI.Constraints
                {
                    X = new Relative(0.5f),
                    Y = new Relative(0.25f),
                    Width = new Relative(0.5f),
                    Height = new Relative(0.5f),
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        RenderCommand<PixelTexture> child = commands[1];

        Assert.Equal(100, child.Rect.Position.X);
        Assert.Equal(50, child.Rect.Position.Y);
        Assert.Equal(100, child.Rect.Size.X);
        Assert.Equal(100, child.Rect.Size.Y);
    }

    [Fact]
    public void FitAndShrinkConstraintsPreserveChildrenSize()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(200, 200);

        using (ui.Element("fitParent"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fit(),
                Height = new Shrink(),
            };

            using (ui.Element("child"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(60),
                    Height = new Fixed(40),
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        RenderCommand<PixelTexture> parent = commands[0];

        Assert.Equal(60, parent.Rect.Size.X);
        Assert.Equal(40, parent.Rect.Size.Y);
    }

    [Fact]
    public void PropertyAssignmentAfterChildrenPreservesLayoutAndMinSize()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(200, 200);

        using (ui.Element("parent"))
        {
            using (ui.Element("child1"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(40),
                    Height = new Fixed(30),
                };
            }

            using (ui.Element("child2"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(50),
                    Height = new Fixed(20),
                };
            }

            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Spacing = 10;
            ui.Padding = new Padding(5);
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        RenderCommand<PixelTexture> parent = commands[0];

        Assert.Equal(50 + 10, parent.Rect.Size.X);
        Assert.Equal(30 + 20 + 10 + 10, parent.Rect.Size.Y);
    }

    [Fact]
    public void WrappedTextUpdatesVerticalParentAndGrandparentAutoHeight()
    {
        TextEngine textEngine = CreateTextEngine();
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(200, 200, textEngine, controller);

        using (ui.Element("grandparent"))
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
            };

            using (ui.Element("parent"))
            {
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fill(),
                };

                using (ui.Text("Hello world this text wraps onto multiple lines"))
                {
                    ui.ID = "wrappedText";
                }
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        RenderCommand<PixelTexture> grandparent = commands[0];
        RenderCommand<PixelTexture> parent = commands[1];
        RenderCommand<PixelTexture> text = commands[2];

        Assert.True(ui.GetTextLayout("wrappedText").Lines.Length > 1);
        Assert.Equal(text.Rect.Size.Y, parent.Rect.Size.Y);
        Assert.Equal(parent.Rect.Size.Y, grandparent.Rect.Size.Y);
    }

    [Fact]
    public void WrappedTextPrecedingFillChildAllocatesRemainingHeightCorrectly()
    {
        TextEngine textEngine = CreateTextEngine();
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(200, 200, textEngine, controller);

        using (ui.Element("container"))
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(200),
            };

            using (ui.Text("Hello world this text wraps onto multiple lines")) { }

            using (ui.Element("fillChild"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        RenderCommand<PixelTexture> container = commands[0];
        RenderCommand<PixelTexture> text = commands[1];
        RenderCommand<PixelTexture> fillChild = commands[2];

        Assert.Equal(container.Rect.Size.Y, text.Rect.Size.Y + fillChild.Rect.Size.Y);
    }

    [Fact]
    public void DistributeFillChildrenHandlesUnevenInitialSizesAndRemaindersWithoutOverflow()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(101, 50);

        using (ui.Element("row"))
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(101),
                Height = new Fixed(50),
            };

            using (ui.Element("a"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                    MinWidth = 30,
                };
            }

            using (ui.Element("b"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            using (ui.Element("c"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        int totalChildWidth = commands[1].Rect.Size.X + commands[2].Rect.Size.X + commands[3].Rect.Size.X;

        Assert.Equal(101, totalChildWidth);
    }

    [Fact]
    public void CrossAxisShrinkClampsAllOverflowingChildren()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(200, 50);

        using (ui.Element("row"))
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(200),
                Height = new Fixed(50),
            };

            using (ui.Element("tall1"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(50),
                    Height = new Fixed(80),
                    MinHeight = 20,
                };
            }

            using (ui.Element("tall2"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(50),
                    Height = new Fixed(100),
                    MinHeight = 20,
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);

        Assert.Equal(50, commands[1].Rect.Size.Y);
        Assert.Equal(50, commands[2].Rect.Size.Y);
    }

    [Fact]
    public void MaxWidthAndMaxHeightClampElementSize()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(200, 200);

        using (ui.Element("parent"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(200),
                Height = new Fixed(200),
            };

            using (ui.Element("capped"))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                    MaxWidth = 120,
                    MaxHeight = 80,
                };
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);

        Assert.Equal(120, commands[1].Rect.Size.X);
        Assert.Equal(80, commands[1].Rect.Size.Y);
    }

    [Fact]
    public void ScrollContainerDoesNotShrinkOverflowingChildrenAndReportsContentSize()
    {
        UIBuilder<PixelTexture> ui = CreateBuilder(100, 100);

        using (ui.Element("scrollView"))
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.VerticalScroll = true;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(100),
                Height = new Fixed(100),
            };

            for (var i = 0; i < 3; i++)
            {
                using (ui.Element($"item_{i}"))
                {
                    ui.Constraints = new UI.Constraints
                    {
                        Width = new Fixed(100),
                        Height = new Fixed(60),
                        MinHeight = 20,
                    };
                }
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        ElementBounds bounds = ui.GetElementBounds("scrollView");

        Assert.Equal(100, bounds.Rect.Size.Y);
        Assert.Equal(180, bounds.ContentSize.Y);
        Assert.Equal(60, commands[1].Rect.Size.Y);
    }

    private static UIBuilder<PixelTexture> CreateBuilder(int width, int height)
    {
        return new UIBuilder<PixelTexture>(width, height, CreateTextEngine(), new UIController());
    }

    private static TextEngine CreateTextEngine()
    {
        var font = new FontInfo("salmon-mono-9-regular", "TestFiles/Fonts/Salmon Mono 9 Regular.ttf");
        return new TextEngine([font]);
    }
}
