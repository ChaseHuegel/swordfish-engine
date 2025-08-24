using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;
using DryIoc;
using Reef;
using Reef.Constraints;
using Reef.MSDF;
using Reef.Text;
using Reef.UI;
using Swordfish.Library.Util;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class UITests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void WrapTest()
    {
        var salmonFont = new FontInfo("salmon-mono-9-regular", "TestFiles/Fonts/Salmon Mono 9 Regular.ttf");
        var awesomeFont = new FontInfo("fa-6-free-solid", "TestFiles/Fonts/Font Awesome 6 Free Solid.otf");
        FontInfo[] fonts = [ salmonFont, awesomeFont ];

        var textEngine = new TextEngine(fonts);

        var fontOptions = new FontOptions
        {
            ID = salmonFont.ID,
            Size = 16,
        };
        
        string[] lines = textEngine.Wrap(fontOptions, "Hello world!", 83);
        Assert.Equal(2, lines.Length);
    }
    
    [Fact]
    public void UITest()
    {
        var swordfishBmp = new Bitmap("TestFiles/Images/swordfish.png");
        PixelTexture swordfishTexture = BitmapToPixelTexture(swordfishBmp);
        
        var salmonFont = new FontInfo("salmon-mono-9-regular", "TestFiles/Fonts/Salmon Mono 9 Regular.ttf");
        var awesomeFont = new FontInfo("fa-6-free-solid", "TestFiles/Fonts/Font Awesome 6 Free Solid.otf");

        var textEngine = new TextEngine([ salmonFont, awesomeFont ]);

        textEngine.TryGetTypeface(salmonFont, out ITypeface typeface);
        AtlasInfo atlasInfo = typeface!.GetAtlasInfo();
        var salmonBmp = new Bitmap(atlasInfo.Path);
        
        textEngine.TryGetTypeface(awesomeFont, out typeface);
        atlasInfo = typeface!.GetAtlasInfo();
        var faBmp = new Bitmap(atlasInfo.Path);
        
        var fonts = new Dictionary<string, PixelFontSDF>
        {
            { salmonFont.ID, new PixelFontSDF(BitmapToPixelTexture(salmonBmp), 0.02f) },
            { awesomeFont.ID, new PixelFontSDF(BitmapToPixelTexture(faBmp), 1f) },
        };

        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(width: 1920, height: 1080, textEngine, controller);

        //  Frame 1, no input
        controller.Update(763, 536, UIController.MouseButtons.None);
        RenderTestUI(ui, awesomeFont, swordfishTexture);
        ui.Build();

        //  Frame 2, clicked
        controller.Update(763, 536, UIController.MouseButtons.Left);
        RenderTestUI(ui, awesomeFont, swordfishTexture);
        ui.Build();
        
        //  Frame 3, no input
        controller.Update(763, 536, UIController.MouseButtons.None);
        RenderTestUI(ui, awesomeFont, swordfishTexture);
        RenderCommand<PixelTexture>[] renderCommands = ui.Build();
        
        var renderer = new PixelRenderer(ui.Width, ui.Height, textEngine, fonts);
        renderer.Render(renderCommands);
        
        using Bitmap bitmap = PixelsToBitmap(ui.Width, ui.Height, renderer.GetPixels());
        bitmap.Save("ui.bmp");
    }

    private static Bitmap PixelsToBitmap(int width, int height, ReadOnlyCollection<Vector4> pixels)
    {
        var bitmap = new Bitmap(width, height);
        for (var i = 0; i < pixels.Count; i++)
        {
            int x = i % width;
            int y = i / width;

            Vector4 pixel = pixels[i];
            Color color = Color.FromArgb(
                alpha: (byte)(pixel.W * 255),
                red: (byte)(pixel.X * 255),
                green: (byte)(pixel.Y * 255),
                blue: (byte)(pixel.Z * 255)
            );
            
            bitmap.SetPixel(x, y, color);
        }

        return bitmap;
    }
    
    private static PixelTexture BitmapToPixelTexture(Bitmap bitmap)
    {
        var texture = new PixelTexture(bitmap.Width, bitmap.Height);
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            Color color = bitmap.GetPixel(x, y);
            var vector4 = new Vector4(
                color.R / 255f,
                color.G / 255f,
                color.B / 255f,
                color.A / 255f
            );
            
            texture.SetPixel(x, y, vector4);
        }
        
        return texture;
    }

    private static void RenderTestUI(UIBuilder<PixelTexture> ui, FontInfo awesomeFont, PixelTexture swordfishTexture)
    {
        using (ui.Element())
        {
            ui.Color = new Vector4(0f, 0.15f, 0.25f, 1f);
            ui.CornerRadius = new CornerRadius(topLeft: 20, topRight: 20, bottomLeft: 20, bottomRight: 20);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Spacing = 4;
            ui.Padding = new Padding(
                left: 20,
                top: 20,
                right: 20,
                bottom: 20
            );
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Right,
                X = new Relative(0.98f),
                Y = new Relative(0.5f),
                Width = new Fixed(200),
                Height = new Fixed(600),
            };

            ui.VerticalScroll = true;
            ui.ScrollY += 200;

            for (var i = 0; i < 50; i++)
            {
                using (ui.Text($"Item {i}")) { }
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
            ui.Constraints = new Constraints
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
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(64),
                    Height = new Fixed(64),
                };
            }
            
            using (ui.Image(swordfishTexture))
            {
                ui.BackgroundColor = new Vector4(0f, 1f, 0f, 1f);
                ui.Constraints = new Constraints
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
            ui.Constraints = new Constraints
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
                ui.Constraints = new Constraints
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
            ui.Constraints = new Constraints
            {
                X = new Relative(0.25f),
                Y = new Relative(0.25f),
                Width = new Fixed(200),
                Height = new Fixed(200),
            };
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0.5f, 0f, 0f, 1f);
                ui.Constraints = new Constraints
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
                ui.Constraints = new Constraints
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
                ui.Constraints = new Constraints
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
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
                Width = new Fixed(500),
            };
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }

            if (ui.Button("test"))
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
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        X = new Relative(0.5f),
                        Y = new Relative(0.5f),
                    };
                }

                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                ui.Constraints = new Constraints
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
            ui.Constraints = new Constraints
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
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(50),
                        Height = new Fixed(50),
                    };
                    
                    using (ui.Text(i.ToString())) { }

                    using (ui.Element())
                    {
                        ui.Color = new Vector4(1f);
                        ui.Constraints = new Constraints
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