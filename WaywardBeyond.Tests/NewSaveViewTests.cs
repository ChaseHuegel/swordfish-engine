using System;
using System.Collections.Generic;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Tests;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Data;
using Xunit;

namespace WaywardBeyond.Tests;

public class NewSaveViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    [Fact]
    public void GalaxyFormMatchesLegacy()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.AssertPageMatchesLegacy($"{width}x{height}-new-save", width, height, ui =>
            {
                var view = new NewSaveView(new TestLocalization()) { Input = new FakeInputService() };
                view.Render(ui, CreateState());
            });
        }
    }

    [Fact]
    public void CreationOverlayMatchesSnapshot()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.Compare($"{width}x{height}-new-save-creating", width, height, ui =>
            {
                var view = new NewSaveView(new TestLocalization()) { Input = new FakeInputService() };
                view.Render(ui, new NewSaveState(
                    Creating: true,
                    Name: string.Empty,
                    Seed: string.Empty,
                    Mode: GameMode.Creative,
                    CanCreate: false,
                    StatusKey: string.Empty,
                    StatusDanger: false,
                    StatusWarning: false,
                    Tab: 0,
                    [
                        new CreateLineState("Seeding terrain", 0.3),
                        new CreateLineState("Carving rivers", 0.18),
                        new CreateLineState("Placing ruins", 0.06),
                    ],
                    CreateElapsed: 0d
                ));
            }, allowUpdate: true);
        }
    }

    [Fact]
    public void ReadyFormHidesTheEmptyStatus()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.Compare($"{width}x{height}-new-save-ready", width, height, ui =>
            {
                var view = new NewSaveView(new TestLocalization()) { Input = new FakeInputService() };
                view.Render(ui, CreateReadyState());
            });
        }
    }

    [Fact]
    public void ReadyFooterKeepsCreateOnTheRight()
    {
        var view = new NewSaveView(new TestLocalization()) { Input = new FakeInputService() };
        string actual = UiSnapshotTests.CaptureRender(1280, 720, ui => view.Render(ui, CreateReadyState()));
        Assert.True(LeftOf(actual, "id=Button_Create ") > 900, "Create did not stay on the right side of the footer.");
    }

    [Fact]
    public void ClickingAdventureModeSelectsAdventure()
    {
        var controller = new UIController();
        var view = new NewSaveView(new TestLocalization()) { Input = new FakeInputService() };
        GameMode? selected = null;
        view.GameModeSelected = mode => selected = mode;
        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);
        NewSaveState state = CreateReadyState(GameMode.Creative);

        controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0d);

        ElementBounds option = ui.GetElementBounds("Choice_GameMode.1");
        Assert.True(option.Rect.Size.X > 0 && option.Rect.Size.Y > 0, "Adventure option has no bounds.");
        int x = option.Rect.Position.X + (option.Rect.Size.X / 2);
        int y = option.Rect.Position.Y + (option.Rect.Size.Y / 2);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, state);
        ui.Build(0d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0d);

        Assert.Equal(GameMode.Adventure, selected);
    }

    [Fact]
    public void AdventureModeRendersAdventureTelemetryAndDescription()
    {
        var view = new NewSaveView(new TestLocalization()) { Input = new FakeInputService() };
        string actual = UiSnapshotTests.CaptureRender(1280, 720, ui => view.Render(ui, CreateReadyState(GameMode.Adventure)));

        Assert.Contains("text=ui.label.level.mode: Adventure", actual, StringComparison.Ordinal);
        Assert.Contains("text=ui.text.level.mode.adventure", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void NarrowLayoutKeepsFooterVisibleAndFormScrollable()
    {
        var controller = new UIController();
        var view = new NewSaveView(new TestLocalization()) { Input = new FakeInputService() };
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), controller);

        for (var frame = 0; frame < 3; frame++)
        {
            controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
            view.FormScroll.Measure(ui, "NewSave.Form");
            view.Render(ui, CreateReadyState(GameMode.Creative));
            ui.Build(0d);
        }

        ElementBounds createButton = ui.GetElementBounds("Button_Create");
        Assert.True(createButton.Rect.Size.Y > 0, "Create button was clipped out at 800x600.");
        Assert.True(createButton.Rect.Bottom <= 480, $"Create button overflowed the 800x600 page (bottom={createButton.Rect.Bottom}).");

        ElementBounds form = ui.GetElementBounds("NewSave.Form");
        int x = form.Rect.Position.X + (form.Rect.Size.X / 2);
        int y = form.Rect.Position.Y + (form.Rect.Size.Y / 2);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.FormScroll.Measure(ui, "NewSave.Form");
        view.Render(ui, CreateReadyState(GameMode.Creative));
        ui.Build(0d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None, scrollDeltaY: -1f);
        view.FormScroll.Measure(ui, "NewSave.Form");
        view.Render(ui, CreateReadyState(GameMode.Creative));
        ui.Build(0d);

        Assert.True(view.FormScroll.Offset > 0, "Narrow galaxy form did not scroll when overflowing.");
    }

    private static int LeftOf(string snapshot, string marker)
    {
        foreach (string line in snapshot.Split('\n'))
        {
            if (line.Contains(marker, StringComparison.Ordinal))
            {
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(line, @" rect=(-?\d+),");
                return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new Xunit.Sdk.XunitException($"No command matches '{marker}'.");
    }

    private static NewSaveState CreateReadyState(GameMode mode = GameMode.Creative)
    {
        return new NewSaveState(
            Creating: false,
            "Outer Hercules VI",
            "Lunar Steer Glorifies Wormhole",
            mode,
            CanCreate: true,
            StatusKey: string.Empty,
            StatusDanger: false,
            StatusWarning: false,
            Tab: 0,
            [],
            CreateElapsed: 0d
        );
    }

    /// <summary>Builds the empty form state that the legacy capture used.</summary>
    private static NewSaveState CreateState(GameMode mode = GameMode.Creative)
    {
        return new NewSaveState(
            Creating: false,
            Name: string.Empty,
            Seed: string.Empty,
            mode,
            CanCreate: false,
            StatusKey: "ui.message.name.required",
            StatusDanger: true,
            StatusWarning: false,
            Tab: 0,
            [],
            CreateElapsed: 0d
        );
    }
}
