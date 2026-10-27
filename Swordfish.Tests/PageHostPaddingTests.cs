using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Config;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Guards the page frame against the padding of the menu host.</summary>
public class PageHostPaddingTests
{
    [Theory]
    [InlineData(800, 600)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void PageFrameIgnoresTheMenuHostPadding(int width, int height)
    {
        RenderCommand<Material> atRoot = Find(Render(width, height, hosted: false), "SettingsPage");
        List<RenderCommand<Material>> hostedCommands = Render(width, height, hosted: true);
        RenderCommand<Material> hosted = Find(hostedCommands, "SettingsPage");

        Assert.Equal(atRoot.Rect.Size, hosted.Rect.Size);

        RenderCommand<Material> apply = Find(hostedCommands, "Settings.Apply");
        Assert.True(
            apply.Rect.Bottom <= hosted.Rect.Bottom,
            $"The apply button ends at {apply.Rect.Bottom}, below the page at {hosted.Rect.Bottom}."
        );
    }

    private static List<RenderCommand<Material>> Render(int width, int height, bool hosted)
    {
        var page = new SettingsPage<MenuPage>(
            MenuPage.Settings,
            configManager: null!,
            controlSettings: new ControlSettings(),
            windowSettings: new WindowSettings(),
            renderSettings: new RenderSettings(),
            volumeSettings: new VolumeSettings(),
            gameplayConfig: new GameplayConfig(),
            uiSettings: new UISettings(),
            soundEffectService: null!,
            windowContext: new FakeWindowContext(),
            inputService: new FakeInputService(),
            localization: new TestLocalization()
        );
        page.OnEnter();

        var ui = new UIBuilder<Material>(width, height, new StubTextEngine(), new UIController());
        if (hosted)
        {
            RenderIntoHost(ui, page);
        }
        else
        {
            page.RenderPage(delta: 0d, ui, menu: null!);
        }

        return ui.Build(delta: 0d);
    }

    /// <summary>Copies the vertical stack, inset, and fill spacers of <c>TitleMenu.RenderUI</c>.</summary>
    private static void RenderIntoHost(UIBuilder<Material> ui, IMenuPage<MenuPage> page)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding(left: 20, top: 64, right: 20, bottom: 20);
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            page.RenderPage(delta: 0d, ui, menu: null!);

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
        }
    }

    private static RenderCommand<Material> Find(List<RenderCommand<Material>> commands, string id)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            if (commands[i].ID == id)
            {
                return commands[i];
            }
        }

        throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
    }
}
