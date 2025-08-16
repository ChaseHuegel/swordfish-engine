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
        var ui = new UIBuffer();
        
        using (ui.Element())
        {
            ui.BackgroundColor = new Vector4(1f);
            ui.Constraints = new Constraints
            {
                Width = new Fixed(50),
                Height = new Fixed(50),
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
                Width = new Fixed(200),
                Height = new Fixed(100),
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

        ui.Build();
    }
}