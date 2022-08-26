using System;
using System.Drawing;
using System.Net.Mime;
using System.Timers;
using ImGuiNET;
using Swordfish.Library.Types.Constraints;
using Swordfish.Types.Constraints;
using Swordfish.UI.Elements;

namespace Swordfish.Demo.UI
{
    public static class TestUI
    {
        public static void CreateCanvas()
        {
            CanvasElement myCanvas = new("UI Test Canvas")
            {
                Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove,
                Constraints = new RectConstraints
                {
                    Anchor = ConstraintAnchor.TOP_CENTER,
                    X = new AbsoluteConstraint(0),
                    Y = new AbsoluteConstraint(0),
                    Width = new RelativeConstraint(0.5f),
                    Height = new RelativeConstraint(0.5f)
                },
                Content = {
                    new LayoutGroup {
                        Constraints = new RectConstraints
                        {
                            Anchor = ConstraintAnchor.TOP_CENTER,
                            X = new AbsoluteConstraint(0),
                            Y = new AbsoluteConstraint(0),
                            Width = new RelativeConstraint(1f),
                            Height = new AbsoluteConstraint(12)
                        },
                        Content = {
                            new TextElement("This"),
                            new TextElement("is"),
                            new TextElement("a"),
                            new TextElement("layout"),
                            new TextElement("group"),
                        }
                    },
                    CreateTextPanel(),
                }
            };
        }

        #region Text Panel
        public static IElement CreateTextPanel()
        {
            return new PanelElement("Text Panel")
            {
                Constraints = new RectConstraints
                {
                    Anchor = ConstraintAnchor.TOP_RIGHT,
                    X = new AbsoluteConstraint(0),
                    Y = new AbsoluteConstraint(0),
                    Width = new RelativeConstraint(1f),
                    Height = new RelativeConstraint(0.5f)
                },
                Tooltip = new Tooltip
                {
                    Help = true,
                    Text = "This is a panel for testing text elements."
                },
                Content = {
                    new TextElement("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Aliquet sagittis id consectetur purus ut faucibus pulvinar elementum. Libero enim sed faucibus turpis in eu. Mi in nulla posuere sollicitudin. Purus in massa tempor nec feugiat nisl pretium. In eu mi bibendum neque egestas congue. Dui nunc mattis enim ut tellus elementum. Cursus euismod quis viverra nibh cras pulvinar mattis nunc sed. Sed vulputate mi sit amet mauris commodo quis. Phasellus vestibulum lorem sed risus. Amet aliquam id diam maecenas ultricies mi."),
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
}
