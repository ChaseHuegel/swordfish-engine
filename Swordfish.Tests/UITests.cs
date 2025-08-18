using System.Drawing;
using System.Numerics;
using Reef;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class UITests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void UITest()
    {
        var ui = new UIBuilder<byte[]>(1920, 1080);
        
        using (ui.Element())
        {
            ui.BackgroundColor = new Vector4(0f, 0f, 0f, 1f);
            ui.Constraints = new Constraints
            {
                Width = new Fixed(300),
                Height = new Fixed(300),
            };
            
            using (ui.Element())
            {
                ui.BackgroundColor = new Vector4(1f, 0f, 0f, 0.5f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(500),
                    Height = new Fixed(500),
                    MinWidth = 10,
                    MinHeight = 10,
                };
            }
            
            using (ui.Element())
            {
                ui.BackgroundColor = new Vector4(0f, 1f, 0f, 0.5f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(500),
                    Height = new Fixed(500),
                    MinWidth = 10,
                    MinHeight = 10,
                };
            }
        }

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.BackgroundColor = new Vector4(1f);
            ui.Constraints = new Constraints
            {
                X = new Relative(0.25f),
                Y = new Relative(0.25f),
                Width = new Fixed(200),
                Height = new Fixed(200),
            };
            
            using (ui.Element())
            {
                ui.BackgroundColor = new Vector4(0.5f, 0f, 0f, 1f);
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
                ui.BackgroundColor = new Vector4(0f, 0.5f, 0f, 1f);
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
                ui.BackgroundColor = new Vector4(0f, 0f, 0.5f, 1f);
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
            ui.BackgroundColor = new Vector4(0.25f, 0.25f, 0.25f, 1f);
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
                ui.BackgroundColor = new Vector4(0f, 0.5f, 0.5f, 1f);
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                };
            }
            
            using (ui.Element())
            {
                ui.BackgroundColor = new Vector4(0f, 0.5f, 0.5f, 0f);
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
            
            using (ui.Element())
            {
                ui.BackgroundColor = new Vector4(0f, 0.5f, 0.5f, 1f);
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
            ui.BackgroundColor = new Vector4(0.25f, 0.25f, 0.25f, 1f);
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
                    ui.BackgroundColor = new Vector4(0f, 0.5f, 0.5f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(50),
                        Height = new Fixed(50),
                    };
                }
            }
        }

        RenderCommand<byte[]>[] renderCommands = ui.Build();
        using var bitmap = new Bitmap(ui.Width, ui.Height);
        foreach (RenderCommand<byte[]> renderCommand in renderCommands)
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
                bitmap.SetPixel(x, y, AlphaBlend(currentColor, color));
            }
        }
        bitmap.Save("ui.bmp");
        return;
        
        Color AlphaBlend(Color background, Color foreground)
        {
            float af = foreground.A / 255f;
            float ab = background.A / 255f;

            float ar = af + ab * (1 - af);
            if (ar <= 0)
            {
                return Color.FromArgb(0, 0, 0, 0); // fully transparent
            }

            var r = (byte)((foreground.R * af + background.R * ab * (1 - af)) / ar);
            var g = (byte)((foreground.G * af + background.G * ab * (1 - af)) / ar);
            var b = (byte)((foreground.B * af + background.B * ab * (1 - af)) / ar);
            var a = (byte)(ar * 255);

            return Color.FromArgb(a, r, g, b);
        }
    }
}