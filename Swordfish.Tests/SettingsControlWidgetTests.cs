using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Settings;
using Xunit;

namespace Swordfish.Tests;

public class SettingsControlWidgetTests
{
    [Fact]
    public void ChoiceWidgetReportsTheSelectedIndex()
    {
        var chosen = -1;
        var controller = new UIController();
        var ui = new UIBuilder<Material>(400, 80, new StubTextEngine(), controller);

        Click(ui, controller, 300, 40, () => Render(ui, chosen: 0, value => chosen = value));

        Assert.Equal(1, chosen);
    }

    [Fact]
    public void SliderWidgetReportsTheDraggedValue()
    {
        float value = 0.5f;
        var controller = new UIController();
        var ui = new UIBuilder<Material>(400, 80, new StubTextEngine(), controller);

        controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
        Render(ui, 0, v => value = v);
        ui.Build(0f);

        controller.UpdateMouse(300, 40, UIController.MouseButtons.Left);
        Render(ui, 0, v => value = v);
        ui.Build(0.1f);
        Render(ui, 0, v => value = v);
        ui.Build(0.2f);

        Assert.NotEqual(0.5f, value);
    }

    private static void Click(UIBuilder<Material> ui, UIController controller, int x, int y, System.Action render)
    {
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        render();
        ui.Build(0f);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        render();
        ui.Build(0.1f);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        render();
        ui.Build(0.2f);
    }

    private static void Render(UIBuilder<Material> ui, int chosen, System.Action<int> onChanged)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                X = new Fixed(20),
                Y = new Fixed(24),
                Width = new Fixed(360),
                Height = new Fixed(32),
            };
            SettingsChoiceWidget.SettingsChoice(
                ui,
                "test",
                ["Off", "On"],
                chosen == 0 ? 0 : 1,
                enabled: true,
                new Constraints { Width = new Fixed(360), Height = new Fixed(32) },
                (_, index) => onChanged(index)
            );
        }
    }
}
