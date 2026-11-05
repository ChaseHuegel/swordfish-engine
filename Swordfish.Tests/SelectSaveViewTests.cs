using System.Collections.Generic;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

public class SelectSaveViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    /// <summary>The star chart needs a measured field, so the capture renders two frames.</summary>
    [Fact]
    public void GalaxyArchivePageMatchesSnapshots()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            var scroll = new ScrollAreaState();
            var view = new SelectSaveView(new TestLocalization(), scroll);
            SelectSaveState state = CreateState(view);

            UiSnapshotTests.Compare(
                $"page-select-save-{width}x{height}",
                width,
                height,
                ui => view.Render(ui, state),
                frames: 2
            );
        }
    }

    /// <summary>Selecting from the archive list must work in both directions.</summary>
    [Fact]
    public void ArchiveRowClickSelectsTheSave()
    {
        var controller = new UIController();
        var scroll = new ScrollAreaState();
        var view = new SelectSaveView(new TestLocalization(), scroll);
        string? selected = null;
        view.SaveSelected = guid => selected = guid;
        SelectSaveState state = CreateState(view);
        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);

        // Settle the scroll metrics so the row geometry stops moving.
        for (var i = 0; i < 3; i++)
        {
            controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
            view.Render(ui, state);
            ui.Build(0d);
        }

        ElementBounds row = ui.GetElementBounds("Button_SelectSave_Frontier Base");
        Assert.True(row.Rect.Size.X > 0, "The archive row has no bounds.");
        int x = row.Rect.Position.X + (row.Rect.Size.X / 2);
        int y = row.Rect.Position.Y + (row.Rect.Size.Y / 2);

        for (var i = 0; i < 2; i++)
        {
            controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            view.Render(ui, state);
            ui.Build(0d);
        }

        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, state);
        ui.Build(0d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        List<RenderCommand<Material>> commands = ui.Build(0d);

        Assert.True(selected != null, "No row click fired. Interactive elements at the point: " + DescribeBlockers(commands, x, y));
        Assert.Equal("save-a", selected);
    }

    /// <summary>Labels size to their text instead of a fixed width.</summary>
    [Fact]
    public void ChartLabelsMeasureTheirText()
    {
        var controller = new UIController();
        var scroll = new ScrollAreaState();
        var view = new SelectSaveView(new TestLocalization(), scroll);
        SelectSaveState state = CreateState(view);
        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);

        for (var frame = 0; frame < 3; frame++)
        {
            controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
            view.Measure(ui, state);
            view.Render(ui, state);
            ui.Build(0d);
        }

        ElementBounds longLabel = ui.GetElementBounds("SelectSave.Label.save-a");
        ElementBounds shortLabel = ui.GetElementBounds("SelectSave.Label.save-b");

        Assert.True(longLabel.Rect.Size.X > 0, "The label was not measured.");
        Assert.True(shortLabel.Rect.Size.X > 0, "The label was not measured.");
        Assert.True(longLabel.Rect.Size.X < StarChartLayout.LABEL_WIDTH);
        Assert.True(longLabel.Rect.Size.X > shortLabel.Rect.Size.X);
    }

    /// <summary>The trash icon inside the row must still fire.</summary>
    [Fact]
    public void ArchiveRowDeleteFires()
    {
        var controller = new UIController();
        var scroll = new ScrollAreaState();
        var view = new SelectSaveView(new TestLocalization(), scroll);
        string? deleted = null;
        view.SaveDeleteRequested = guid => deleted = guid;
        SelectSaveState state = CreateState(view);
        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);

        controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0d);

        ElementBounds trash = ui.GetElementBounds("Button_DeleteSave_Frontier Base");
        int x = trash.Rect.Position.X + (trash.Rect.Size.X / 2);
        int y = trash.Rect.Position.Y + (trash.Rect.Size.Y / 2);
        Assert.True(trash.Rect.Size.X > 0, "Trash has no bounds.");

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, state);
        ui.Build(0d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0d);

        Assert.Equal("save-a", deleted);
    }

    /// <summary>Lists the interactive elements under a point, top-most first.</summary>
    private static string DescribeBlockers(List<RenderCommand<Material>> commands, int x, int y)
    {
        var found = new List<string>();
        for (int i = commands.Count - 1; i >= 0; i--)
        {
            RenderCommand<Material> command = commands[i];
            if (command.ID == null)
            {
                continue;
            }

            IntRect rect = command.Rect;
            if (x >= rect.Position.X && x < rect.Position.X + rect.Size.X
                && y >= rect.Position.Y && y < rect.Position.Y + rect.Size.Y)
            {
                found.Add(command.ID);
            }
        }

        return string.Join(", ", found);
    }

    /// <summary>Builds the state that the legacy capture used: two archives with the first active.</summary>
    private static SelectSaveState CreateState(SelectSaveView view)
    {
        var rows = new List<SaveRowState>
        {
            new(
                "save-a",
                "Frontier Base",
                GameMode.Creative,
                true,
                0,
                true,
                1_234_567_890L,
                3_600_000L,
                "adjective42 subject0 verb0 destination0"
            ),
            new(
                "save-b",
                "Ashfall",
                GameMode.Adventure,
                false,
                0,
                false,
                987_654_321L,
                120_000L,
                "obsidian apple ignites hyperion"
            ),
        };

        return new SelectSaveState(
            Remote: false,
            CanCreateSave: true,
            Tab: 0,
            HasActive: true,
            rows
        );
    }
}
