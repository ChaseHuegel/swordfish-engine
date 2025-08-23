using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
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
        
        var salmonFont = new FontInfo("salmon-mono-9-regular", "TestFiles/Fonts/Salmon Mono 9 Regular.ttf");
        var awesomeFont = new FontInfo("fa-6-free-solid", "TestFiles/Fonts/Font Awesome 6 Free Solid.otf");
        FontInfo[] fonts = [ salmonFont, awesomeFont ];

        var textEngine = new TextEngine(fonts);

        textEngine.TryGetTypeface(salmonFont, out ITypeface typeface);
        AtlasInfo atlasInfo = typeface!.GetAtlasInfo();
        var salmonBmp = new Bitmap(atlasInfo.Path);
        
        textEngine.TryGetTypeface(awesomeFont, out typeface);
        atlasInfo = typeface!.GetAtlasInfo();
        var faBmp = new Bitmap(atlasInfo.Path);

        var controller = new UIController();
        controller.Update(952, 536, UIController.MouseButtons.Left);
        
        var ui = new UIBuilder<Bitmap>(width: 1920, height: 1080, textEngine, controller);

        RenderTestUI(ui, awesomeFont, swordfishBmp);
        ui.Build();
        RenderTestUI(ui, awesomeFont, swordfishBmp);
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
            
            Color backgroundColor = Color.FromArgb(
                alpha: (int)(renderCommand.BackgroundColor.W * 255),
                red: (int)(renderCommand.BackgroundColor.X * 255),
                green: (int)(renderCommand.BackgroundColor.Y * 255),
                blue: (int)(renderCommand.BackgroundColor.Z * 255)
            );
            
            //  Render rects
            for (int x = renderCommand.Rect.Left; x <= renderCommand.Rect.Right; x++)
            for (int y = renderCommand.Rect.Top; y <= renderCommand.Rect.Bottom; y++)
            {
                //  Skip pixels that are outside the corner radii
                var skipPixel = false;
                CornerRadius radius = renderCommand.CornerRadius;
                if (x < renderCommand.Rect.Left + radius.TopLeft && y < renderCommand.Rect.Top + radius.TopLeft)
                {
                    int dx = x - (renderCommand.Rect.Left + radius.TopLeft);
                    int dy = y - (renderCommand.Rect.Top + radius.TopLeft);
                    skipPixel = dx * dx + dy * dy > radius.TopLeft * radius.TopLeft;
                }
                else if (x > renderCommand.Rect.Right - radius.TopRight && y < renderCommand.Rect.Top + radius.TopRight)
                {
                    int dx = x - (renderCommand.Rect.Right - radius.TopRight);
                    int dy = y - (renderCommand.Rect.Top + radius.TopRight);
                    skipPixel = dx * dx + dy * dy > radius.TopRight * radius.TopRight;
                }
                else if (x > renderCommand.Rect.Right - radius.BottomRight && y > renderCommand.Rect.Bottom - radius.BottomRight)
                {
                    int dx = x - (renderCommand.Rect.Right - radius.BottomRight);
                    int dy = y - (renderCommand.Rect.Bottom - radius.BottomRight);
                    skipPixel = dx * dx + dy * dy > radius.BottomRight * radius.BottomRight;
                }
                else if (x < renderCommand.Rect.Left + radius.BottomLeft && y > renderCommand.Rect.Bottom - radius.BottomLeft)
                {
                    int dx = x - (renderCommand.Rect.Left + radius.BottomLeft);
                    int dy = y - (renderCommand.Rect.Bottom - radius.BottomLeft);
                    skipPixel = dx * dx + dy * dy > radius.BottomLeft * radius.BottomLeft;
                }

                if (skipPixel)
                {
                    continue;
                }

                Color currentColor = bitmap.GetPixel(x, y);

                //  Render a textured rect
                if (renderCommand.TextureData != null)
                {
                    var u = (int)MathS.RangeToRange(x, renderCommand.Rect.Left, renderCommand.Rect.Right, 0, renderCommand.TextureData.Width - 1);
                    var v = (int)MathS.RangeToRange(y, renderCommand.Rect.Top, renderCommand.Rect.Bottom, 0, renderCommand.TextureData.Height - 1);
                    Color sample = renderCommand.TextureData.GetPixel(u, v);
                    sample = MultiplyBlend(sample, color);
                    //  Blend in any texture background color
                    sample = AlphaBlend(backgroundColor, sample);
                    
                    bitmap.SetPixel(x, y, AlphaBlend(currentColor, sample));
                }
                //  Render a color rect
                else
                {
                    //  If text is present, treat this as a background.
                    Color rectColor = renderCommand.Text == null ? color : backgroundColor;
                    bitmap.SetPixel(x, y, AlphaBlend(currentColor, rectColor));
                }
            }
            
            if (renderCommand.Text == null)
            {
                continue;
            }

            //  Render text
            TextLayout textLayout = textEngine.Layout(renderCommand.FontOptions, renderCommand.Text, renderCommand.Rect.Size.X);
            for (var i = 0; i < textLayout.Glyphs.Length; i++)
            {
                GlyphLayout glyph = textLayout.Glyphs[i];
                    
                var bbox = new IntRect(
                    renderCommand.Rect.Left + glyph.BBOX.Left,
                    renderCommand.Rect.Top + glyph.BBOX.Top,
                    renderCommand.Rect.Left + glyph.BBOX.Right,
                    renderCommand.Rect.Top + glyph.BBOX.Bottom
                );

                DrawCharacter(bitmap, bbox, glyph.UV, color, renderCommand.FontOptions.ID);
            }
        }
        bitmap.Save("ui.bmp");
        return;
        
        void DrawCharacter(Bitmap bitmap, IntRect bbox, IntRect uv, Color color, string fontID)
        {
            for (int x = bbox.Left; x < bbox.Right; x++)
            for (int y = bbox.Top; y < bbox.Bottom; y++)
            {
                var u = (int)MathS.RangeToRange(x, bbox.Left, bbox.Right, uv.Left, uv.Right);
                var v = (int)MathS.RangeToRange(y, bbox.Top, bbox.Bottom, uv.Top, uv.Bottom);
                Color currentColor = bitmap.GetPixel(x, y);
                Color sample = SampleSdf(fontID == awesomeFont.ID ? faBmp : salmonBmp, fontID == awesomeFont.ID ? 1f : 0.02f, u, v, Color.Transparent, color);
                bitmap.SetPixel(x, y, AlphaBlend(currentColor, sample));
            }
        }

        Color SampleSdf(Bitmap sdf, float fwidth, int u, int v, Color outsideColor, Color insideColor)
        {
            Color s = sdf.GetPixel(u, v);
            float d = Median(s.R / 255f, s.G / 255f, s.B / 255f) - 0.5f;
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

    private static void RenderTestUI(UIBuilder<Bitmap> ui, FontInfo awesomeFont, Bitmap swordfishBmp)
    {
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
            
            using (ui.Image(swordfishBmp))
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(64),
                    Height = new Fixed(64),
                };
            }
            
            using (ui.Image(swordfishBmp))
            {
                ui.BackgroundColor = new Vector4(0f, 1f, 0f, 1f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }
        }
        
        using (ui.Image(swordfishBmp))
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
            
            using (ui.Element())
            {
                if (ui.Clicked("test"))
                {
                    ui.Color = new Vector4(0f, 0f, 0f, 1f);
                    using (ui.Text("Clicked!")) { }
                }
                else
                {
                    ui.Color = new Vector4(0f, 0.5f, 0.5f, 0f);
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