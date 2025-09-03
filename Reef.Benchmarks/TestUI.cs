using System.Numerics;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;

namespace Reef.Benchmarks;

internal static class TestUI
{
    public static void Populate(UIBuilder<PixelTexture> ui, FontInfo awesomeFont, PixelTexture swordfishTexture)
    {
        using (ui.Element())
        {
            ui.Color = new Vector4(0f, 0.15f, 0.25f, 1f);
            ui.CornerRadius = new CornerRadius(topLeft: 20, topRight: 20, bottomLeft: 20, bottomRight: 20);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding(
                left: 20,
                top: 20,
                right: 20,
                bottom: 20
            );
            ui.Constraints = new UI.Constraints
            {
                Anchors = Anchors.Center | Anchors.Right,
                X = new Relative(0.98f),
                Y = new Relative(0.5f),
                Width = new Fixed(200),
                Height = new Fixed(600),
            };
            
            ui.ClipConstraints = new UI.Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            ui.VerticalScroll = true;
            ui.ScrollY -= 214;

            for (var i = 0; i < 50; i++)
            {
                using (ui.Text($"Item {i} and some more extra text to wrap")) { }
            }
        }

        using (ui.Element())
        {
            ui.CornerRadius = new CornerRadius(topLeft: 0, topRight: 50, bottomLeft: 100, bottomRight: 10);
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Color = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(400),
                Height = new Fixed(300),
            };
            
            using (ui.Text("Hello world!"))
            {
                ui.FontSize = 10;
                ui.Color = new Vector4(1f, 0f, 0f, 1f);
            }
            
            using (ui.Text("\uf24e \uf2bd \uf03e \uf118 \uf164"))
            {
                ui.FontOptions = new FontOptions { ID = awesomeFont.ID, Size = 20 };
            }
            
            using (ui.Text("This is a little bit of a longer piece of text with a background color."))
            {
                ui.BackgroundColor = new Vector4(0f, 0f, 1f, 1f);
            }
            
            using (ui.Image(swordfishTexture))
            {
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(64),
                    Height = new Fixed(64),
                };
            }
            
            using (ui.Image(swordfishTexture))
            {
                ui.BackgroundColor = new Vector4(0f, 1f, 0f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }
        }
        
        using (ui.Image(swordfishTexture))
        {
            ui.Padding = new Padding(8, 8, 8, 8);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.BackgroundColor = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new UI.Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.1f),
                Width = new Fixed(128),
                Height = new Fixed(128),
            };

            using (ui.Text("Swordfish"))
            {
                ui.BackgroundColor = new Vector4(1f, 0f, 0f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    Anchors = Anchors.Center,
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                };
            }
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = new Vector4(1f);
            ui.Constraints = new UI.Constraints
            {
                X = new Relative(0.25f),
                Y = new Relative(0.25f),
                Width = new Fixed(200),
                Height = new Fixed(200),
            };
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0.5f, 0f, 0f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    X = new Relative(0f),
                    Y = new Relative(0f),
                    Width = new Fixed(100),
                    Height = new Fixed(100),
                };
            }
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    X = new Relative(0.5f),
                    Y = new Relative(0.5f),
                    Width = new Fixed(100),
                    Height = new Fixed(100),
                };
            }
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0f, 0.5f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    X = new Relative(0.5f),
                    Y = new Relative(0f),
                    Width = new Relative(0.5f),
                    Height = new Relative(0.5f),
                };
            }
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 1f);
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Constraints = new UI.Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
                Width = new Fixed(500),
            };
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }

            if (ui.Button(id: "button1", text: "test"))
            {
                using (ui.Text("Clicked!")) { }
            }
            
            using (ui.Element())
            {
                if (ui.Clicked("click me"))
                {
                    ui.Color = new Vector4(0f, 0f, 0f, 1f);
                }
                else
                {
                    ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                }

                using (ui.Text("Click me"))
                {
                    ui.Constraints = new UI.Constraints
                    {
                        Anchors = Anchors.Center,
                        X = new Relative(0.5f),
                        Y = new Relative(0.5f),
                    };
                }

                ui.Constraints = new UI.Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                ui.Constraints = new UI.Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }
        }
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 1f);
            ui.Spacing = 8;
            ui.Padding = new Padding(
                left: 8,
                top: 8,
                right: 8,
                bottom: 8
            );
            ui.Constraints = new UI.Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                X = new Relative(0.5f),
                Y = new Relative(0.99f),
            };
        
            for (var i = 0; i < 6; i++)
            {
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
                    ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                    ui.Constraints = new UI.Constraints
                    {
                        Width = new Fixed(50),
                        Height = new Fixed(50),
                    };
                    
                    using (ui.Text(i.ToString())) { }

                    using (ui.Element())
                    {
                        ui.Color = new Vector4(1f);
                        ui.Constraints = new UI.Constraints
                        {
                            Anchors = Anchors.Bottom | Anchors.Right,
                            X = new Relative(1f),
                            Y = new Relative(1f),
                            Width = new Fixed(20),
                            Height = new Fixed(20),
                        };
                    }
                }
            }
        }
    }
}