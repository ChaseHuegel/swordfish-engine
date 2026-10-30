using System;
using System.Collections.Generic;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using Xunit;

namespace Swordfish.Tests;

public class LegacyHomeTests
{
    [Fact]
    public void HomeWithoutSessionMatchesLegacy()
    {
        int[] widths = [800, 1280, 1920];
        int[] heights = [600, 720, 1080];
        for (var i = 0; i < widths.Length; i++)
        {
            int width = widths[i];
            int height = heights[i];
            UiSnapshotTests.AssertPageMatchesLegacy($"{width}x{height}-home", width, height, ui =>
            {
                CreateHome().RenderPage(0d, ui, null!);
            });
        }
    }

    [Fact]
    public void HoverCardTopAlignsWithContinueInsideMenuLayout()
    {
        var home = HomeFixtures.CreateHome(true);
        string actual = UiSnapshotTests.CaptureHovered(
            800,
            600,
            ui =>
            {
                using (ui.Element())
                {
                    ui.Padding = new Padding { Left = 20, Top = 64, Right = 20, Bottom = 20 };
                    ui.Constraints = new Constraints
                    {
                        Width = new Relative(1f),
                        Height = new Relative(1f),
                    };
                    home.RenderPage(0d, ui, null!);
                }
            },
            "Button_Continue");

        int continueTop = TopOf(actual, "id=Button_Continue ");
        int cardTop = TopOf(actual, "caps=10:2:");
        Assert.Equal(continueTop, cardTop);
    }

    private static int TopOf(string snapshot, string marker)
    {
        foreach (string line in snapshot.Split('\n'))
        {
            if (line.Contains(marker, StringComparison.Ordinal))
            {
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(line, @" rect=-?\d+,(-?\d+),");
                return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new Xunit.Sdk.XunitException($"No command matches '{marker}'.");
    }

    [Fact]
    public void HomeWithSessionHoveredMatchesLegacy()
    {
        int[] widths = [800, 1280, 1920];
        int[] heights = [600, 720, 1080];
        for (var i = 0; i < widths.Length; i++)
        {
            int width = widths[i];
            int height = heights[i];
            var home = HomeFixtures.CreateHome(true);
            string actual = UiSnapshotTests.CaptureHovered(width, height, ui => home.RenderPage(0d, ui, null!), "Button_Continue");
            UiSnapshotTests.AssertMatchesLegacy($"{width}x{height}-home-session-hovered", actual);
        }
    }

    [Fact]
    public void HomeWithSessionMatchesLegacy()
    {
        int[] widths = [800, 1280, 1920];
        int[] heights = [600, 720, 1080];
        for (var i = 0; i < widths.Length; i++)
        {
            int width = widths[i];
            int height = heights[i];
            var home = HomeFixtures.CreateHome(true);
            string actual = UiSnapshotTests.CaptureRender(width, height, ui => home.RenderPage(0d, ui, null!));
            UiSnapshotTests.AssertMatchesLegacy($"{width}x{height}-home-session", actual);
        }
    }

    private static HomePage CreateHome()
    {
        var localization = new TestLocalization();
        var modalMenu = new ModalMenu(
            null!,
            null!,
            new StubShortcutService(),
            null!,
            [new StubModalPage(PlaytestNoticeModal.Modal)]
        );

        return new HomePage(
            null!,
            null!,
            localization,
            new GameSaveService(null!, null!, null!, null!, null!),
            null!,
            new StubCharacterStorage(),
            null!,
            null!,
            null!,
            new ProfileSettings(),
            new ContinueService(null!, null!, new ProfileSettings(), null!, null!, null!, new StubCharacterStorage(), localization, null!),
            null!,
            modalMenu,
            null!
        );
    }

    private sealed class StubShortcutService : IShortcutService
    {
        public bool RegisterShortcut(Shortcut shortcut) => true;
    }

    private sealed class StubModalPage(Modal id) : IMenuPage<Modal>
    {
        public Modal ID => id;

        public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu) => Result.FromSuccess();
    }

    private sealed class StubCharacterStorage : ICharacterStorage
    {
        public Result<Character> GetCharacter(ulong id) => throw new NotSupportedException();

        public IReadOnlyList<Character> GetAllCharacters() => [];

        public Result SaveCharacter(Character character) => throw new NotSupportedException();

        public Result DeleteCharacter(ulong id) => throw new NotSupportedException();
    }
}
