using System.Drawing;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;

namespace Swordfish.Demo.UI;

public static class TestUI
{
    private static IUIContext UIContext => uiContext ??= SwordfishEngine.Kernel.Get<IUIContext>();
    private static IWindowContext WindowContext => windowContext ??= SwordfishEngine.Kernel.Get<IWindowContext>();

    private static IUIContext? uiContext;
    private static IWindowContext? windowContext;

    public static void CreateCanvas()
    {
        CanvasElement myCanvas = new(UIContext, "UI Test Canvas")
        {
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.CENTER,
                X = new AbsoluteConstraint(0),
                Y = new AbsoluteConstraint(0),
                Width = new CenterConstraint(),
                Height = new CenterConstraint()
            },
            Content = {
                CreateTextPanel(),
                CreateLayoutGroupPanel(),
            }
        };
    }

    #region Layout Panel
    public static IElement CreateLayoutGroupPanel()
    {
        return new PanelElement("LayoutGroup Panel")
        {
            Constraints = new RectConstraints
            {
                Height = new RelativeConstraint(0.3f)
            },
            Tooltip = new Tooltip
            {
                Help = true,
                Text = "This is a panel for testing LayoutGroup elements."
            },
            Content = {
                    new LayoutGroup {
                        Layout = ElementAlignment.HORIZONTAL,
                        ContentSeparator = ContentSeparator.DIVIDER,
                        Constraints = new RectConstraints
                        {
                            Anchor = ConstraintAnchor.TOP_CENTER,
                            Width = new FillConstraint(),
                            Height = new AbsoluteConstraint(UIContext.FontDisplaySize)
                        },
                        Content = {
                            new TextElement("This"),
                            new TextElement("is"),
                            new TextElement("a"),
                            new TextElement("horizontal"),
                            new TextElement("layout"),
                        }
                    },
                    new LayoutGroup {
                        Layout = ElementAlignment.HORIZONTAL,
                        Constraints = new RectConstraints
                        {
                            Height = new AbsoluteConstraint(100),
                            Width = new FillConstraint(),
                        },
                        Content = {
                            new LayoutGroup {
                                Layout = ElementAlignment.VERTICAL,
                                ContentSeparator = ContentSeparator.DIVIDER,
                                Constraints = new RectConstraints
                                {
                                    Width = new RelativeConstraint(0.25f),
                                },
                                Content = {
                                    new TextElement("These"),
                                    new TextElement("vertical"),
                                    new TextElement("are"),
                                    new TextElement("a"),
                                    new TextElement("layout"),
                                }
                            },
                            new LayoutGroup {
                                Layout = ElementAlignment.VERTICAL,
                                ContentSeparator = ContentSeparator.DIVIDER,
                                Constraints = new RectConstraints
                                {
                                    Width = new RelativeConstraint(0.25f),
                                },
                                Content = {
                                    new TextElement("two"),
                                    new TextElement("layouts"),
                                    new TextElement("inside"),
                                    new TextElement("horizontal"),
                                    new TextElement("group"),
                                }
                            },
                            new LayoutGroup {
                                Layout = ElementAlignment.HORIZONTAL,
                                ContentSeparator = ContentSeparator.NONE,
                                Constraints = new RectConstraints
                                {
                                    Anchor = ConstraintAnchor.TOP_LEFT,
                                    Width = new FillConstraint(),
                                },
                                Content = {
                                    new TextElement("This is a horizontal layout."),
                                }
                            },
                        }
                    },
                }
        };
    }
    #endregion

    #region Text Panel
    public static IElement CreateTextPanel()
    {
        return new PanelElement("Text Panel")
        {
            Constraints = new RectConstraints
            {
                Height = new RelativeConstraint(0.5f)
            },
            Tooltip = new Tooltip
            {
                Help = true,
                Text = "This is a panel for testing Text elements."
            },
            Content = {
                new TextElement("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Aliquet sagittis id consectetur purus ut faucibus pulvinar elementum. Libero enim sed faucibus turpis in eu. Mi in nulla posuere sollicitudin. Purus in massa tempor nec feugiat nisl pretium. In eu mi bibendum neque egestas congue. Dui nunc mattis enim ut tellus elementum. Cursus euismod quis viverra nibh cras pulvinar mattis nunc sed. Sed vulputate mi sit amet mauris commodo quis. Phasellus vestibulum lorem sed risus. Amet aliquam id diam maecenas ultricies mi."),
                new DividerElement(),
                new TextElement("Disabled text.") {
                    Enabled = false
                },
                new TextElement("-Bullet 1 (no whitespace)"),
                new TextElement("- Bullet 2 (spaced)"),
                new TextElement("-  Bullet 3 (tabbed)") {
                    Tooltip = new Tooltip {
                        Text = "Bullets are created by simply preceding with a '-'"
                    }
                },
                new TextElement("Non visible text.") {
                    Visible = false
                },
                new TextElement("Some colored text!") {
                    Color = Color.Green,
                    Tooltip = new Tooltip {
                        Help = true,
                        Text = "There is text above this that has Visible = false."
                    }
                },
                new TextElement("This text has a label.") {
                    Label = "MyLabel",
                    Tooltip = new Tooltip {
                        Text = "This has a tooltip."
                    }
                },
                new TextElement("This one has a label and a help tooltip.") {
                    Label = "MyOtherLabel",
                    Tooltip = new Tooltip {
                        Help = true,
                        Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Aliquet sagittis id consectetur purus ut faucibus pulvinar elementum. Libero enim sed faucibus turpis in eu. Mi in nulla posuere sollicitudin. Purus in massa tempor nec feugiat nisl pretium. In eu mi bibendum neque egestas congue. Dui nunc mattis enim ut tellus elementum. Cursus euismod quis viverra nibh cras pulvinar mattis nunc sed. Sed vulputate mi sit amet mauris commodo quis. Phasellus vestibulum lorem sed risus. Amet aliquam id diam maecenas ultricies mi."
                    }
                },
                new TextElement("This text is horizontally aligned.") {
                    Alignment = ElementAlignment.HORIZONTAL
                },
            }
        };
    }
    #endregion
}
