using WaywardBeyond.Client.UI.Layers.Menus.Main;
using Xunit;

namespace Swordfish.Tests;

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
                view.Render(ui, CreateState(view, height));
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
                    ModeLine: string.Empty,
                    SeedLine: string.Empty,
                    TypeLine: string.Empty,
                    CanCreate: false,
                    Status: null,
                    StatusDanger: false,
                    StatusWarning: false,
                    Tab: 0,
                    [
                        new CreateLineState("Seeding terrain", 1f),
                        new CreateLineState("Carving rivers", 0.6f),
                        new CreateLineState("Placing ruins", 0.2f),
                    ],
                    CursorOn: true
                ));
            }, allowUpdate: true);
        }
    }

    /// <summary>Builds the empty form state that the legacy capture used.</summary>
    private static NewSaveState CreateState(NewSaveView view, int height)
    {
        return new NewSaveState(
            Creating: false,
            view.DisplayName(string.Empty),
            view.ModeLine(),
            view.Labeled("ui.label.level.seed", view.DisplaySeed(string.Empty)),
            view.Labeled("ui.label.level.type", "ui.label.level.type.asteroids"),
            CanCreate: false,
            "ui.message.name.required",
            StatusDanger: true,
            StatusWarning: false,
            Tab: 0,
            [],
            CursorOn: false
        );
    }
}
