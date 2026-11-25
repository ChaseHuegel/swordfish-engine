#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Reef;
using Reef.UI;
using SmartFormat;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Identity;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Blueprints;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Voxels;
using Xunit;
using ClientGameState = WaywardBeyond.Client.GameState;
using ClientHost = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Tests the <see cref="BlueprintLibraryView"/> markup interactions and <see cref="BlueprintLibraryLayer"/> lifecycle.</summary>
[Collection("ClientGameState")]
public class BlueprintLibraryViewTests
{
    [Fact]
    public void EmptyStateAndSaveVisibilityFollowStateFlags()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);
        var view = new BlueprintLibraryView(new TestLocalization(), new FakeInputService(), new ScrollAreaState());

        var state = new BlueprintLibraryState(
            new TextBoxState(string.Empty),
            new TextBoxState(string.Empty),
            new TextBoxState(string.Empty))
        {
            CanSave = false,
            Items = [],
        };

        view.Render(ui, state);
        List<RenderCommand<Material>> commands = ui.Build(0.1d);

        Assert.Contains(commands, c => c.Text == "ui.blueprint.empty");
        Assert.DoesNotContain(commands, c => c.Text == "ui.blueprint.save.section");

        state.CanSave = true;
        state.ErrorKey = "ui.blueprint.error.nameRequired";
        view.Render(ui, state);
        commands = ui.Build(0.2d);

        Assert.Contains(commands, c => c.Text == "ui.blueprint.save.section");
        Assert.Contains(commands, c => c.Text == "ui.blueprint.error.nameRequired");
    }

    [Fact]
    public void ReadOnlyBundledBlueprintHidesRenameAndDeleteButtonsAndShowsBuiltInTag()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);
        var view = new BlueprintLibraryView(new TestLocalization(), new FakeInputService(), new ScrollAreaState());

        var state = new BlueprintLibraryState(
            new TextBoxState(string.Empty),
            new TextBoxState(string.Empty),
            new TextBoxState(string.Empty))
        {
            Items =
            [
                new BlueprintListItemState("bundled:builtin/mainMenu", "Main Menu Ship", 11, 7, 16, 130, IsRenaming: false, ConfirmingDelete: false, IsReadOnly: true),
            ],
        };

        view.Render(ui, state);
        List<RenderCommand<Material>> commands = ui.Build(0.1d);

        Assert.Contains(commands, c => c.Text == "ui.blueprint.load");
        Assert.DoesNotContain(commands, c => c.Text == "ui.blueprint.rename");
        Assert.Contains(commands, c => c.Text != null && c.Text.Contains("ui.blueprint.builtIn", StringComparison.Ordinal));
    }

    [Fact]
    public void ViewButtonsInvokeSaveLoadRenameDeleteConfirmationAndCloseCallbacks()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);
        var view = new BlueprintLibraryView(new TestLocalization(), new FakeInputService(), new ScrollAreaState());

        var saveClicks = 0;
        var closeClicks = 0;
        var cancelRenameClicks = 0;
        var cancelDeleteClicks = 0;
        string? loadedId = null;
        string? startRenameId = null;
        string? commitRenameId = null;
        string? requestDeleteId = null;
        string? confirmDeleteId = null;

        view.SaveRequested = () => saveClicks++;
        view.CloseRequested = () => closeClicks++;
        view.LoadRequested = id => loadedId = id;
        view.StartRenameRequested = id => startRenameId = id;
        view.CommitRenameRequested = id => commitRenameId = id;
        view.CancelRenameRequested = () => cancelRenameClicks++;
        view.RequestDeleteRequested = id => requestDeleteId = id;
        view.ConfirmDeleteRequested = id => confirmDeleteId = id;
        view.CancelDeleteRequested = () => cancelDeleteClicks++;

        var state = new BlueprintLibraryState(
            new TextBoxState(string.Empty),
            new TextBoxState("MyDock"),
            new TextBoxState("MyDock Renamed"))
        {
            CanSave = true,
            Items =
            [
                new BlueprintListItemState("bp-1", "Dock", 4, 2, 3, 10, IsRenaming: false, ConfirmingDelete: false),
            ],
        };

        ClickElement(controller, ui, view, state, "Button_SaveBlueprint");
        ClickElement(controller, ui, view, state, "Button_LoadBlueprint_bp-1");
        ClickElement(controller, ui, view, state, "Button_RenameBlueprint_bp-1");
        ClickElement(controller, ui, view, state, "Button_DeleteBlueprint_bp-1");
        ClickElement(controller, ui, view, state, "Button_CloseBlueprints");

        Assert.Equal(1, saveClicks);
        Assert.Equal("bp-1", loadedId);
        Assert.Equal("bp-1", startRenameId);
        Assert.Equal("bp-1", requestDeleteId);
        Assert.Equal(1, closeClicks);

        // Two-step delete confirmation row
        state.Items =
        [
            new BlueprintListItemState("bp-1", "Dock", 4, 2, 3, 10, IsRenaming: false, ConfirmingDelete: true),
        ];
        ClickElement(controller, ui, view, state, "Button_ConfirmDeleteBlueprint_bp-1");
        ClickElement(controller, ui, view, state, "Button_CancelDeleteBlueprint_bp-1");
        Assert.Equal("bp-1", confirmDeleteId);
        Assert.Equal(1, cancelDeleteClicks);

        // Inline rename row
        state.Items =
        [
            new BlueprintListItemState("bp-1", "Dock", 4, 2, 3, 10, IsRenaming: true, ConfirmingDelete: false),
        ];
        ClickElement(controller, ui, view, state, "Button_ConfirmRenameBlueprint_bp-1");
        ClickElement(controller, ui, view, state, "Button_CancelRenameBlueprint_bp-1");
        Assert.Equal("bp-1", commitRenameId);
        Assert.Equal(1, cancelRenameClicks);
    }

    [Fact]
    public void LayerShortcutDisablesWhenOpenAndFiltersBySearchQuery()
    {
        using var dir = new TempDirectory();
        var shortcuts = new CapturingShortcutService();
        var input = new FakeInputService();
        var windowContext = new FakeWindowContext();
        var world = new World();
        var registry = new TestBrickRegistry();
        ushort panelId = registry.Bind("wb:panel");
        var brickDb = new EmptyBrickDatabase();
        var storage = new FileBlueprintStorage(
            NullLogger<FileBlueprintStorage>.Instance,
            new StoragePaths(new StorageConfig(), AppPaths.ForDirectory(new PathInfo(dir.Path))),
            new Migrator([]));

        var voxelObj = new VoxelObject(16);
        voxelObj.Set(0, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(voxelObj, default, default);
        VoxelRegionData encoded = VoxelCodec.Encode(extracted.Region, registry);
        Assert.True(storage.Save("Alpha Hangar", in encoded, 1).Success);
        Assert.True(storage.Save("Beta Outpost", in encoded, 1).Success);

        var motion = new ClientPlayerMotionProcessor(input, windowContext, new FakePhysics(), new EventInvoker<PlayerMovedEvent>([]));
        var interactionState = new InteractionState(motion);
        var inbound = new ClientInbound();
        var profile = new ProfileSettings();
        profile.UserId.Set("bp-ui-test");
        using var rpcHost = new ClientRpcHost(
            new LoopbackRpcServerTransport(),
            inbound,
            new ClientHandlers(inbound),
            new NetworkingConfig(),
            new ProfileUserClaimProvider(profile),
            NullLoggerFactory.Instance);
        var notifications = new NotificationService(NullLogger<NotificationService>.Instance, windowContext);
        try
        {
            var formatter = new LocalizedFormatter(new TestLocalization(), Smart.CreateDefaultSmartFormat());
            var toolState = new RegionToolState();
            var toolService = new RegionToolService(
                input,
                shortcuts,
                new FakePhysics(),
                new FakeRenderContext(world.DataStore),
                windowContext,
                new PlayerData(new EmptyItemDatabase()),
                interactionState,
                toolState,
                new RegionClient(rpcHost, NullLogger<RegionClient>.Instance),
                storage,
                registry,
                brickDb,
                notifications,
                formatter,
                NullLogger<RegionToolService>.Instance);

            var layer = new BlueprintLibraryLayer(
                shortcuts,
                input,
                interactionState,
                storage,
                toolState,
                toolService,
                notifications,
                formatter,
                new TestLocalization(),
                NullLogger<BlueprintLibraryLayer>.Instance);

            Shortcut openShortcut = shortcuts.Find("Blueprint Library");
            Shortcut closeShortcut = shortcuts.Find("Close Blueprint Library");

            ClientHost.GameState.Set(ClientGameState.Playing);
            try
            {
                Assert.True(openShortcut.IsEnabled());
                openShortcut.Action();

                Assert.True(layer.IsVisible());
                // While open, 'B' must NOT trigger openShortcut so typing 'b' in TextBox inputs works cleanly
                Assert.False(openShortcut.IsEnabled());
                Assert.True(closeShortcut.IsEnabled());

                var controller = new UIController();
                var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), controller);

                layer.RenderUI(0.1d, ui);
                List<RenderCommand<Material>> allItems = ui.Build(0.1d);
                Assert.Contains(allItems, c => c.Text == "Alpha Hangar");
                Assert.Contains(allItems, c => c.Text == "Beta Outpost");

                // Click Search box (hover -> press -> release) and type "beta" (includes lowercase 'b')
                ElementBounds searchBounds = ui.GetElementBounds("TextBox_BlueprintSearch");
                int sx = searchBounds.Rect.Left + Math.Max(1, searchBounds.Rect.Size.X / 2);
                int sy = searchBounds.Rect.Top + Math.Max(1, searchBounds.Rect.Size.Y / 2);
                controller.UpdateMouse(sx, sy, UIController.MouseButtons.None);
                layer.RenderUI(0.12d, ui);
                ui.Build(0.12d);
                controller.UpdateMouse(sx, sy, UIController.MouseButtons.Left);
                layer.RenderUI(0.14d, ui);
                ui.Build(0.14d);
                controller.UpdateMouse(sx, sy, UIController.MouseButtons.None);
                layer.RenderUI(0.16d, ui);
                ui.Build(0.16d);
                controller.UpdateInputBuffer(
                [
                    new UIController.Input('b'),
                    new UIController.Input('e'),
                    new UIController.Input('t'),
                    new UIController.Input('a'),
                ]);
                layer.RenderUI(0.2d, ui);
                ui.Build(0.2d);
                controller.UpdateInputBuffer([]);
                layer.RenderUI(0.22d, ui);
                List<RenderCommand<Material>> filtered = ui.Build(0.22d);

                Assert.Contains(filtered, c => c.Text == "Beta Outpost");
                Assert.DoesNotContain(filtered, c => c.Text == "Alpha Hangar");

                closeShortcut.Action();
                Assert.False(layer.IsVisible());
                Assert.False(interactionState.IsInteractionBlocked());
            }
            finally
            {
                ClientHost.GameState.Set(ClientGameState.MainMenu);
            }
        }
        finally
        {
            notifications.Dispose();
        }
    }

    private static void ClickElement(
        UIController controller,
        UIBuilder<Material> ui,
        BlueprintLibraryView view,
        BlueprintLibraryState state,
        string elementId
    )
    {
        controller.UpdateMouse(0, 0, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.1d);

        ElementBounds bounds = ui.GetElementBounds(elementId);
        int x = bounds.Rect.Left + Math.Max(1, bounds.Rect.Size.X / 2);
        int y = bounds.Rect.Top + Math.Max(1, bounds.Rect.Size.Y / 2);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.2d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, state);
        ui.Build(0.3d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.4d);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wb-bpview-{Guid.NewGuid():N}");

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class CapturingShortcutService : IShortcutService
    {
        private readonly List<Shortcut> _shortcuts = [];

        public event Action? BindingsChanged
        {
            add { }
            remove { }
        }

        public bool IsCapturing => false;

        public bool RegisterShortcut(Shortcut shortcut)
        {
            _shortcuts.Add(shortcut);
            return true;
        }

        public bool AddHandler(string id, Func<bool>? isEnabled, Action action, Action? released = null) => true;

        public IReadOnlyList<ShortcutInfo> GetShortcuts() => [];

        public Result<ShortcutBinding> GetBinding(string id) => Result<ShortcutBinding>.FromFailure("Not registered.");

        public bool IsHeld(string id) => false;

        public Result Rebind(string id, ShortcutBinding binding) => Result.FromSuccess();

        public Result ResetBinding(string id) => Result.FromSuccess();

        public void ResetAllBindings() { }

        public void ApplyOverrides(IReadOnlyDictionary<string, ShortcutBinding> overrides) { }

        public IReadOnlyDictionary<string, ShortcutBinding> GetOverrides() => new Dictionary<string, ShortcutBinding>();

        public void BeginCapture(Action<ShortcutCaptureResult> onCompleted) { }

        public void CancelCapture() { }

        public Shortcut Find(string name)
        {
            return _shortcuts.First(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        }
    }

    private sealed class TestBrickRegistry : IBrickRegistry
    {
        private readonly Dictionary<string, ushort> _ids = new(StringComparer.Ordinal);

        public ushort Bind(string id)
        {
            ushort val = (ushort)(_ids.Count + 1);
            _ids[id] = val;
            return val;
        }

        public ushort Id(string name) => _ids.TryGetValue(name, out ushort id) ? id : (ushort)0;

        public string? Name(ushort id) => null;
    }

    private sealed class EmptyBrickDatabase : IBrickDatabase
    {
        public Result<Brick> Get(string id) => Result<Brick>.FromFailure("None");

        public Result<Brick> Get(ushort id) => Result<Brick>.FromFailure("None");

        public List<Brick> Get(Func<Brick, bool> predicate) => [];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class EmptyItemDatabase : IAssetDatabase<Item>
    {
        public Result<Item> Get(string id) => Result<Item>.FromFailure("None");
    }
}
