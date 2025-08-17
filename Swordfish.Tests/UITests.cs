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
        var ui = new UIBuilder<byte[]>();

        using (ui.Element())
        {
            ui.BackgroundColor = new Vector4(1f);
            ui.Constraints = new Constraints
            {
                X = new Relative(0.25f),
                Y = new Relative(0.25f),
                Width = new Fixed(100),
                Height = new Fixed(100),
            };
        }

        using (ui.Element())
        {
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
                X = new Relative(0.5f),
                Y = new Relative(0.5f),
            };

            for (var i = 0; i < 2; i++)
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
        using var bitmap = new Bitmap(width: 1920, height: 1080);
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
                bitmap.SetPixel(x, y, color);
            }
        }
        bitmap.Save("ui.bmp");
    }
}