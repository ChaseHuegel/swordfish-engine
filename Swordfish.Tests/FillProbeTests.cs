using System;
using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class FillProbeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(LayoutDirection.None, false)]
    [InlineData(LayoutDirection.Vertical, false)]
    [InlineData(LayoutDirection.None, true)]
    [InlineData(LayoutDirection.Vertical, true)]
    [InlineData(LayoutDirection.Horizontal, true)]
    public void Probe(LayoutDirection parentLayout, bool childHasContent)
    {
        var ui = new UIBuilder<Material>(800, 600, null!, new UIController());
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints { X = new Fixed(0), Y = new Fixed(0), Width = new Fixed(600), Height = new Fixed(100) };
            using (ui.Element())
            {
                ui.LayoutDirection = parentLayout;
                ui.Constraints = new Constraints { X = new Fixed(100), Y = new Fixed(0), Width = new Fixed(204), Height = new Fixed(50) };
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Constraints = new Constraints { Width = new Fill(), Height = new Fill() };
                    if (childHasContent)
                    {
                        using (ui.Element())
                        {
                            ui.Constraints = new Constraints { Width = new Fixed(296), Height = new Fixed(10) };
                        }
                    }
                }
            }
        }

        List<RenderCommand<Material>> commands = ui.Build(0d);
        foreach (var c in commands)
            output.WriteLine($"{parentLayout} content={childHasContent}: rect={c.Rect.Left},{c.Rect.Top},{c.Rect.Size.X},{c.Rect.Size.Y}");
    }
}
