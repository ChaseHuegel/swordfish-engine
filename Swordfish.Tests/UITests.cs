using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Mime;
using System.Numerics;
using Reef;
using Swordfish.Library.Util;
using Typography.OpenFont;
using Typography.TextLayout;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class UITests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void RenderTest()
    {
        const string fontFilePath = "TestFiles/Fonts/Salmon Mono 9 Regular.ttf";
        var ui = new UIBuilder<Bitmap>(1920, 1080, fontFilePath);
        char[] textBuffer = "Hello world!".ToCharArray();
        List<string> lines = ui.WrapText(textBuffer, 0, textBuffer.Length, 83);
        
        Assert.Equal(2, lines.Count);
    }
    
    [Fact]
    public void UITest()
    {
        const string fontFilePath = "TestFiles/Fonts/Salmon Mono 9 Regular.ttf";
        var swordfishBmp = new Bitmap("TestFiles/Images/swordfish.png");
        var fontBmp = new Bitmap("TestFiles/Fonts/font.png");
        
        var ui = new UIBuilder<Bitmap>(1920, 1080, fontFilePath);
        
        using (ui.Element())
        {
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
                ui.Color = new Vector4(1f, 0f, 0f, 1f);
            }
            
            using (ui.Text("Test."))
            {
                ui.Color = new Vector4(0f, 1f, 0f, 1f);
            }
            
            using (ui.Text("This is a little bit of a longer piece of text."))
            {
                ui.Color = new Vector4(0f, 0f, 1f, 1f);
            }
            
            using (ui.Image(swordfishBmp))
            {
                ui.Color = new Vector4(1f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(64),
                    Height = new Fixed(64),
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
            
            using (ui.Element())
            {
                ui.Color = new Vector4(0f, 0.5f, 0.5f, 0f);
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
                    ui.Color = new Vector4(0f, 0.5f, 0.5f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(50),
                        Height = new Fixed(50),
                    };
                }
            }
        }

        RenderCommand<Bitmap>[] renderCommands = ui.Build();
        using var bitmap = new Bitmap(ui.Width, ui.Height);
        foreach (RenderCommand<Bitmap> renderCommand in renderCommands)
        {
            Color color = Color.FromArgb(
                alpha: (int)(renderCommand.Color.W * 255),
                red: (int)(renderCommand.Color.X * 255),
                green: (int)(renderCommand.Color.Y * 255),
                blue: (int)(renderCommand.Color.Z * 255)
            );
            
            for (int x = renderCommand.Rect.Left; x <= renderCommand.Rect.Right; x++)
            for (int y = renderCommand.Rect.Top; y <= renderCommand.Rect.Bottom; y++)
            {
                Color currentColor = bitmap.GetPixel(x, y);

                if (renderCommand.TextureData != null)
                {
                    var u = (int)MathS.RangeToRange(x, renderCommand.Rect.Left, renderCommand.Rect.Right, 0, renderCommand.TextureData.Width - 1);
                    var v = (int)MathS.RangeToRange(y, renderCommand.Rect.Top, renderCommand.Rect.Bottom, 0, renderCommand.TextureData.Height - 1);
                    Color sample = renderCommand.TextureData.GetPixel(u, v);
                    sample = MultiplyBlend(sample, color);
                    
                    bitmap.SetPixel(x, y, AlphaBlend(currentColor, sample));
                }
                else
                {
                    bitmap.SetPixel(x, y, AlphaBlend(currentColor, color));
                }
                
                if (renderCommand.Text != null)
                {
                    var u = (int)MathS.RangeToRange(x, renderCommand.Rect.Left, renderCommand.Rect.Right, 0, fontBmp.Width - 1);
                    var v = (int)MathS.RangeToRange(y, renderCommand.Rect.Top, renderCommand.Rect.Bottom, 0, fontBmp.Height - 1);
                    Color sample = SampleSdf(fontBmp, u, v, Color.Transparent, color);
                    bitmap.SetPixel(x, y, AlphaBlend(currentColor, sample));
                }
                else
                {
                    bitmap.SetPixel(x, y, AlphaBlend(currentColor, color));
                }
            }
        }
        bitmap.Save("ui.bmp");
        return;

        Color SampleSdf(Bitmap sdf, int u, int v, Color outsideColor, Color insideColor)
        {
            Color s = sdf.GetPixel(u, v);
            float d = Median(s.R / 255f, s.G / 255f, s.B / 255f) - 0.5f;
            float fwidth = 0.02f;
            float w = Math.Clamp(d / fwidth + 0.5f, 0f, 1f);
            return Mix(outsideColor, insideColor, w);
        }

        float Median(float a, float b, float c)
        {
            return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
        }
        
        Color AlphaBlend(Color background, Color foreground)
        {
            float af = foreground.A / 255f;
            float ab = background.A / 255f;

            float ar = af + ab * (1 - af);
            if (ar <= 0)
            {
                return Color.FromArgb(0, 0, 0, 0);
            }

            var r = (byte)((foreground.R * af + background.R * ab * (1 - af)) / ar);
            var g = (byte)((foreground.G * af + background.G * ab * (1 - af)) / ar);
            var b = (byte)((foreground.B * af + background.B * ab * (1 - af)) / ar);
            var a = (byte)(ar * 255);

            return Color.FromArgb(a, r, g, b);
        }
        
        Color MultiplyBlend(Color background, Color foreground)
        {
            float af = foreground.A / 255f;
            float ab = background.A / 255f;
            
            float rf = foreground.R / 255f;
            float rb = background.R / 255f;
            
            float gf = foreground.G / 255f;
            float gb = background.G / 255f;
            
            float bf = foreground.B / 255f;
            float bb = background.B / 255f;

            var a = (byte)(af * ab * 255f);
            var r = (byte)(rf * rb * 255f);
            var g = (byte)(gf * gb * 255f);
            var b = (byte)(bf * bb * 255f);

            return Color.FromArgb(a, r, g, b);
        }
        
        Color Mix(Color x, Color y, float t)
        {
            return Color.FromArgb(
                alpha: (int)(x.A + (y.A - x.A) * t),
                red:   (int)(x.R + (y.R - x.R) * t),
                green: (int)(x.G + (y.G - x.G) * t),
                blue:  (int)(x.B + (y.B - x.B) * t)
            );
        }
    }
}