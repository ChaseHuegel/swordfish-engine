#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFormat;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Identity;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Client.UI.Layers.Blueprints;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Voxels;
using Xunit;
using ClientGameState = WaywardBeyond.Client.GameState;
using ClientHost = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Tests <see cref="RegionToolState"/>, <see cref="RegionToolService"/>, and <see cref="RegionToolRadial"/>.</summary>
[Collection("ClientGameState")]
public class RegionToolTests
{
    [Fact]
    public void RegionToolStateTracksAnchorsLimitsTransformCacheAndDisconnectReset()
    {
        var state = new RegionToolState();
        Uuid structA = Uuid.FromValue(101);
        Uuid structB = Uuid.FromValue(102);

        Assert.False(state.SetAnchorA(structA, new Int3(3, 4, 5)));
        Assert.False(state.TryGetCompleteSelection(out _, out _, out _));

        // Setting B on another structure fails without disturbing A
        Assert.False(state.SetAnchorB(structB, new Int3(1, 1, 1)));
        Assert.True(state.SetAnchorB(structA, new Int3(1, 2, 3)));
        Assert.True(state.TryGetCompleteSelection(out Uuid entityUuid, out Int3 min, out Int3 max));
        Assert.Equal(structA, entityUuid);
        Assert.Equal(new Int3(1, 2, 3), min);
        Assert.Equal(new Int3(3, 4, 5), max);

        // Switching entity via Anchor A resets B
        Assert.True(state.SetAnchorA(structB, new Int3(0, 0, 0)));
        Assert.False(state.TryGetCompleteSelection(out _, out _, out _));

        // Bounds exceeding 64-edge fail TryGetCompleteSelection but keep anchor data
        Assert.True(state.SetAnchorB(structB, new Int3(70, 0, 0)));
        Assert.False(state.TryGetCompleteSelection(out _, out _, out _));

        // Clipboard + transform cache + disconnect reset keeps clipboard
        var bricks = BrickRegistry.FromNames(["wb:panel"]);
        var voxelObj = new VoxelObject(16);
        voxelObj.Set(0, 0, 0, new Voxel(bricks.Id("wb:panel"), new ShapeLight(BrickShape.Block, 0), 0));
        voxelObj.Set(1, 0, 0, new Voxel(bricks.Id("wb:panel"), new ShapeLight(BrickShape.Block, 0), 0));
        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(voxelObj, new Int3(0, 0, 0), new Int3(1, 0, 0));

        state.SetClipboard(extracted.Region, extracted.BrickCount, "Wing", enterPasteMode: true);
        Assert.Equal(RegionToolMode.Paste, state.Mode);
        Assert.True(state.TryGetTransformedClipboard(null, out VoxelRegionData cachedFirst, out int rev1, out _, out _));
        Assert.Equal(2, cachedFirst.SizeX);
        Assert.Equal(1, cachedFirst.SizeZ);

        state.RotateYaw();
        Assert.True(state.TryGetTransformedClipboard(null, out VoxelRegionData rotated, out int rev2, out _, out _));
        Assert.Equal(rev1, rev2);
        Assert.Equal(1, rotated.SizeX);
        Assert.Equal(2, rotated.SizeZ);

        state.ResetTransientOnDisconnect();
        Assert.Equal(RegionToolMode.Select, state.Mode);
        Assert.False(state.TryGetSelectionAnchors(out _, out _, out _, out _, out _, out _, out _, out _));
        Assert.True(state.HasClipboard);
    }

    [Fact]
    public void LoadBlueprintToClipboardEquipsProjectorEntersPasteAndNotifies()
    {
        using var dir = new TempDirectory();
        using var harness = new Harness(dir.Path);

        VoxelRegionData encoded = CreateEncodedTwoBrickRegion(harness.BrickRegistry);
        Result<BlueprintSummary> saved = harness.Storage.Save("Shuttle", in encoded, brickCount: 2);
        Assert.True(saved.Success, saved.Message);

        Result<BlueprintData> loaded = harness.ToolService.LoadBlueprintToClipboard(saved.Value.Id);
        Assert.True(loaded.Success, loaded.Message);
        Assert.Equal(RegionToolMode.Paste, harness.ToolState.Mode);
        Assert.True(harness.ToolState.HasClipboard);

        harness.TickPlaying();
        harness.FlushNotifications();
        Assert.Equal(2, harness.PlayerData.GetActiveSlot(harness.World.DataStore));
        Assert.Contains(
            harness.Notifications.GetActiveNotifications(),
            n => n.Notification.Type == NotificationType.Action
                && n.Notification.Text.Contains("Shuttle", StringComparison.Ordinal));
    }

    [Fact]
    public void RadialMenuDoesNotCopyOrCutOnHoverAndOpensBlueprintsOnRelease()
    {
        using var dir = new TempDirectory();
        using var harness = new Harness(dir.Path);
        harness.SelectProjectorSlot();

        // Prepare a complete selection on an in-world structure
        Uuid structureUuid = Uuid.FromValue(5050);
        int structure = harness.World.DataStore.Alloc(structureUuid);
        var voxelObj = new VoxelObject(16);
        voxelObj.Set(0, 0, 0, new Voxel(harness.BrickRegistry.Id("wb:panel"), new ShapeLight(BrickShape.Block, 0), 0));
        harness.World.DataStore.AddOrUpdate(structure, new VoxelComponent(voxelObj));
        harness.ToolState.SetAnchorA(structureUuid, new Int3(0, 0, 0));
        harness.ToolState.SetAnchorB(structureUuid, new Int3(0, 0, 0));

        ClientHost.GameState.Set(ClientGameState.Playing);
        try
        {
            Assert.True(harness.Radial.Available);
            Assert.True(((IActionIndicator)harness.Radial).IsVisible());

            // Pressing opens radial and locks interaction exclusively
            harness.Radial.OnPressed();
            Assert.True(((IUILayer)harness.Radial).IsVisible());
            Assert.True(harness.InteractionState.IsInteractionBlocked());

            // Hovering over Copy (index 1) must NOT execute Copy before release
            harness.Radial.OnNodeEnter(1);
            harness.ToolService.Tick(1f / 60f, harness.World.DataStore);
            Assert.False(harness.ToolState.HasClipboard);

            // Releasing dispatches Copy and frees blocker
            harness.Radial.OnReleased();
            Assert.False(((IUILayer)harness.Radial).IsVisible());
            Assert.False(harness.InteractionState.IsInteractionBlocked());

            harness.ToolService.Tick(1f / 60f, harness.World.DataStore);
            Assert.True(harness.ToolState.HasClipboard);
            Assert.Equal(1, harness.ToolState.ClipboardBrickCount);

            // Opening radial again and releasing on Blueprints (index 7) hands off the exclusive blocker
            harness.Radial.OnPressed();
            harness.Radial.OnNodeEnter(7);
            harness.Radial.OnReleased();
            Assert.False(((IUILayer)harness.Radial).IsVisible());
            Assert.True(harness.Library.IsVisible());
            Assert.True(harness.InteractionState.IsInteractionBlocked());
        }
        finally
        {
            harness.Library.Close();
            ClientHost.GameState.Set(ClientGameState.MainMenu);
        }
    }

    [Fact]
    public void MissingGameModeComponentFailsClosedAndDisconnectDrainsPendingSaves()
    {
        using var dir = new TempDirectory();
        using var harness = new Harness(dir.Path);
        harness.SelectProjectorSlot();

        int playerEntity = -1;
        harness.World.DataStore.Query<PlayerComponent>(0f, (float _, DataStore _, int e, in PlayerComponent _) => playerEntity = e);
        Assert.True(playerEntity >= 0);
        harness.World.DataStore.Remove(typeof(GameModeComponent), playerEntity);

        harness.ToolService.QueueAction(RegionToolAction.Copy);
        harness.TickPlaying();
        harness.FlushNotifications();

        Assert.False(harness.ToolState.HasClipboard);
        Assert.Contains(
            harness.Notifications.GetActiveNotifications(),
            n => n.Notification.Type == NotificationType.Action
                && n.Notification.Text.Contains("notification.region.creativeOnly", StringComparison.Ordinal));

        Result<BlueprintSummary>? pendingResult = null;
        harness.ToolService.QueueSaveBlueprint("Stale Save", res => pendingResult = res);
        ClientHost.GameState.Set(ClientGameState.MainMenu);
        harness.ToolService.Tick(1f / 60f, harness.World.DataStore);

        Assert.True(pendingResult.HasValue);
        Assert.False(pendingResult.Value.Success);
    }

    [Fact]
    public void ToggleOverwriteEmptyEmitsActionNotificationAndFreeSpacePoseMatchesPlayerUp()
    {
        using var dir = new TempDirectory();
        using var harness = new Harness(dir.Path);
        harness.SelectProjectorSlot();

        harness.ToolService.QueueAction(RegionToolAction.ToggleOverwriteAir);
        harness.TickPlaying();
        harness.FlushNotifications();

        Assert.True(harness.ToolState.OverwriteAir);
        Assert.Contains(
            harness.Notifications.GetActiveNotifications(),
            n => n.Notification.Type == NotificationType.Action
                && n.Notification.Text.Contains("notification.region.overwriteAirOn", StringComparison.Ordinal));

        Quaternion rolledPlayer = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f);
        Vector3 cameraPos = new(10f, 20f, 30f);
        Int3 regionSize = new(4, 2, 6);

        RegionPlacementResolver.ComputeFreeSpacePose(
            cameraPos,
            rolledPlayer,
            distance: 12f,
            regionSize,
            out Vector3 spawnPosition,
            out Quaternion spawnOrientation);

        Vector3 expectedUp = Vector3.Transform(Vector3.UnitY, Quaternion.Normalize(rolledPlayer));
        Vector3 actualUp = Vector3.Transform(Vector3.UnitY, spawnOrientation);
        Assert.True(Vector3.Distance(expectedUp, actualUp) < 1e-4f, $"Expected up {expectedUp}, got {actualUp}.");

        Vector3 expectedCenter = cameraPos + Vector3.Transform(-Vector3.UnitZ, Quaternion.Normalize(rolledPlayer)) * 12f;
        var centerOffset = new Int3(-(regionSize.X / 2), -(regionSize.Y / 2), -(regionSize.Z / 2));
        var localBoundsCenter = new Vector3(
            centerOffset.X + (regionSize.X - 1) * 0.5f,
            centerOffset.Y + (regionSize.Y - 1) * 0.5f,
            centerOffset.Z + (regionSize.Z - 1) * 0.5f);
        Vector3 actualCenter = spawnPosition + Vector3.Transform(localBoundsCenter, spawnOrientation);
        Assert.True(Vector3.Distance(expectedCenter, actualCenter) < 1e-4f, $"Expected center {expectedCenter}, got {actualCenter}.");
    }

    [Fact]
    public void ClosingBlueprintLibraryWithEscapeDoesNotPauseWhenResolvedInInjectorOrder()
    {
        using var dir = new TempDirectory();
        using var harness = new Harness(dir.Path);
        var shortcuts = new ShortcutService(harness.Input, NullLogger<ShortcutService>.Instance);

        using var container = new Container();
        container.RegisterInstance<IShortcutService>(shortcuts);
        container.RegisterInstance<IInputService>(harness.Input);
        container.RegisterInstance<IAssetDatabase<Texture>>(new StubTextureDatabase());
        container.RegisterInstance<IAssetDatabase<Shader>>(new StubShaderDatabase());
        container.RegisterInstance(harness.InteractionState);
        container.RegisterInstance(harness.PlayerData);
        container.RegisterInstance<IECSContext>(new TestEcsContext(harness.World));
        container.RegisterInstance<ILocalization>(new RegionLocalization());
        container.RegisterInstance(harness.ToolState);
        container.RegisterInstance(harness.ToolService);
        container.RegisterInstance<IBlueprintStorage>(harness.Storage);
        container.RegisterInstance(harness.Notifications);
        container.RegisterInstance(harness.Formatter);
        container.RegisterInstance<ILogger<BlueprintLibraryLayer>>(NullLogger<BlueprintLibraryLayer>.Instance);

        // Register in the same order as WaywardBeyond.Client/Injector.cs.
        container.Register<RegionToolRadial>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, RegionToolRadial>();
        container.Register<PauseShortcutProbeLayer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, PauseShortcutProbeLayer>();
        container.Register<BlueprintLibraryLayer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, BlueprintLibraryLayer>();

        IUILayer[] layers = container.Resolve<IUILayer[]>();
        Assert.Equal(3, layers.Length);
        BlueprintLibraryLayer library = container.Resolve<BlueprintLibraryLayer>();

        ClientHost.GameState.Set(ClientGameState.Playing);
        try
        {
            library.Open();
            Assert.True(library.IsVisible());

            harness.Input.KeyPressed?.Invoke(harness.Input, new KeyEventArgs(Key.Esc));
            Assert.False(library.IsVisible());
            Assert.Equal(ClientGameState.Playing, ClientHost.GameState.Get());

            harness.Input.KeyPressed?.Invoke(harness.Input, new KeyEventArgs(Key.Esc));
            Assert.Equal(ClientGameState.Paused, ClientHost.GameState.Get());
        }
        finally
        {
            library.Close();
            ClientHost.GameState.Set(ClientGameState.MainMenu);
        }
    }

    [Fact]
    public void PausingPreservesInGameSessionAndProcessesPendingSave()
    {
        using var dir = new TempDirectory();
        using var harness = new Harness(dir.Path);
        VoxelRegionData encoded = CreateEncodedTwoBrickRegion(harness.BrickRegistry);
        VoxelRegionData region = VoxelCodec.Decode(in encoded, harness.BrickRegistry);
        harness.ToolState.SetClipboard(in region, brickCount: 2);

        harness.TickPlaying();
        Result<BlueprintSummary>? saveResult = null;
        harness.ToolService.QueueSaveBlueprint("Paused Blueprint", res => saveResult = res);

        ClientHost.GameState.Set(ClientGameState.Paused);
        try
        {
            harness.ToolService.Tick(1f / 60f, harness.World.DataStore);
        }
        finally
        {
            ClientHost.GameState.Set(ClientGameState.MainMenu);
        }

        Assert.True(saveResult.HasValue);
        Assert.True(saveResult.Value.Success, saveResult.Value.Message);
    }

    private sealed class PauseShortcutProbeLayer : IUILayer
    {
        public PauseShortcutProbeLayer(IShortcutService shortcutService, InteractionState interactionState)
        {
            shortcutService.RegisterShortcut(new Shortcut
            {
                Name = "Toggle paused",
                Category = "General",
                Modifiers = ShortcutModifiers.None,
                Key = Key.Esc,
                IsEnabled = () => ClientHost.GameState >= ClientGameState.Playing,
                Action = () =>
                {
                    if (ClientHost.GameState == ClientGameState.Playing && !interactionState.IsInteractionBlocked())
                    {
                        ClientHost.Pause();
                    }
                },
            });
        }

        public bool IsVisible() => false;

        public Result RenderUI(double delta, Reef.UIBuilder<Material> ui) => Result.FromSuccess();
    }

    private static VoxelRegionData CreateEncodedTwoBrickRegion(IBrickRegistry registry)
    {
        var obj = new VoxelObject(16);
        ushort panelId = registry.Id("wb:panel");
        obj.Set(0, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        obj.Set(1, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(obj, new Int3(0, 0, 0), new Int3(1, 0, 0));
        return VoxelCodec.Encode(extracted.Region, registry);
    }

    private sealed class Harness : IDisposable
    {
        public readonly World World = new();
        public readonly FakeInputService Input = new();
        public readonly StubShortcutService Shortcuts = new();
        public readonly TestBrickRegistry BrickRegistry = new();
        public readonly TestBrickDatabase BrickDatabase = new();
        public readonly FileBlueprintStorage Storage;
        public readonly InteractionState InteractionState;
        public readonly PlayerData PlayerData;
        public readonly NotificationService Notifications;
        public readonly LocalizedFormatter Formatter;
        public readonly RegionToolState ToolState = new();
        public readonly RegionToolService ToolService;
        public readonly BlueprintLibraryLayer Library;
        public readonly RegionToolRadial Radial;

        private readonly ClientRpcHost _rpcHost;
        private readonly FakeWindowContext _windowContext = new();
        private readonly int _player;

        public Harness(string blueprintRoot)
        {
            ushort panelId = BrickRegistry.Bind("wb:panel");
            BrickDatabase.Add(panelId, new Brick("wb:panel", BrickAlpha.Opaque, false, null, BrickShape.Block, default));

            var config = new StorageConfig();
            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(blueprintRoot));
            Storage = new FileBlueprintStorage(
                NullLogger<FileBlueprintStorage>.Instance,
                new StoragePaths(config, appPaths),
                new Migrator([]));

            var projectorItem = new Item(
                "wb:projector",
                "Projector",
                "items/projector.png",
                1,
                placeable: null,
                tool: new ToolDefinition { Type = ToolType.Blueprint },
                viewModel: null,
                worldModel: null);
            var itemDb = new FakeItemDatabase([projectorItem]);

            PlayerData = new PlayerData(itemDb);
            _player = World.DataStore.Alloc();
            World.DataStore.AddOrUpdate(_player, new PlayerComponent());
            World.DataStore.AddOrUpdate(_player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            World.DataStore.AddOrUpdate(_player, new GameModeComponent(GameMode.Creative));
            var inv = new InventoryComponent(36);
            inv.Contents[2] = new ItemData("wb:projector", 1, 1);
            World.DataStore.AddOrUpdate(_player, inv);
            World.DataStore.AddOrUpdate(_player, new EquipmentComponent { ActiveInventorySlot = 0 });

            var motion = new ClientPlayerMotionProcessor(
                Input,
                _windowContext,
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([]));
            InteractionState = new InteractionState(motion);

            var inbound = new ClientInbound();
            var netConfig = new NetworkingConfig();
            var profile = new ProfileSettings();
            profile.UserId.Set("region-tool-test");
            _rpcHost = new ClientRpcHost(
                new LoopbackRpcServerTransport(),
                inbound,
                new ClientHandlers(inbound),
                netConfig,
                new ProfileUserClaimProvider(profile),
                NullLoggerFactory.Instance);
            var regionClient = new RegionClient(_rpcHost, NullLogger<RegionClient>.Instance);

            Notifications = new NotificationService(NullLogger<NotificationService>.Instance, _windowContext);
            var localization = new RegionLocalization();
            Formatter = new LocalizedFormatter(localization, Smart.CreateDefaultSmartFormat());

            ToolService = new RegionToolService(
                Input,
                Shortcuts,
                new FakePhysics(),
                new FakeRenderContext(World.DataStore),
                _windowContext,
                PlayerData,
                InteractionState,
                ToolState,
                regionClient,
                Storage,
                BrickRegistry,
                BrickDatabase,
                Notifications,
                Formatter,
                NullLogger<RegionToolService>.Instance);

            Library = new BlueprintLibraryLayer(
                Shortcuts,
                Input,
                InteractionState,
                Storage,
                ToolState,
                ToolService,
                Notifications,
                Formatter,
                localization,
                NullLogger<BlueprintLibraryLayer>.Instance);

            Radial = new RegionToolRadial(
                Shortcuts,
                new StubTextureDatabase(),
                new StubShaderDatabase(),
                InteractionState,
                PlayerData,
                new TestEcsContext(World),
                localization,
                ToolState,
                ToolService,
                new Lazy<BlueprintLibraryLayer>(() => Library));
        }

        public void SelectProjectorSlot() => PlayerData.SetActiveSlot(World.DataStore, 2);

        public void FlushNotifications() => _windowContext.Update?.Invoke(0.016d);

        public void TickPlaying()
        {
            ClientHost.GameState.Set(ClientGameState.Playing);
            try
            {
                ToolService.Tick(1f / 60f, World.DataStore);
            }
            finally
            {
                ClientHost.GameState.Set(ClientGameState.MainMenu);
            }
        }

        public void Dispose()
        {
            Notifications.Dispose();
            _rpcHost.Dispose();
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wb-rtool-{Guid.NewGuid():N}");

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TestEcsContext(World world) : IECSContext
    {
        public DataBinding<double> Delta { get; } = new();

        public World World { get; } = world;
    }

    private sealed class StubShaderDatabase : IAssetDatabase<Shader>
    {
        public Result<Shader> Get(string id) => Result<Shader>.FromSuccess(new Shader(id));
    }

    private sealed class FakeItemDatabase(Item[] items) : IAssetDatabase<Item>
    {
        private readonly Dictionary<string, Item> _items = items.ToDictionary(i => i.ID, StringComparer.Ordinal);

        public Result<Item> Get(string id) =>
            _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure("Missing item");
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

        public string? Name(ushort id)
        {
            foreach ((string name, ushort value) in _ids)
            {
                if (value == id)
                {
                    return name;
                }
            }

            return null;
        }
    }

    private sealed class TestBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, Brick> _bricks = [];

        public void Add(ushort id, Brick brick) => _bricks[id] = brick;

        public Result<Brick> Get(string id) => Result<Brick>.FromFailure("Not implemented");

        public Result<Brick> Get(ushort id) =>
            _bricks.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure("Unknown brick");

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _bricks.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => true;
    }

    private sealed class RegionLocalization : ILocalization
    {
        public string? GetString(string value) => value switch
        {
            "notification.blueprint.loaded" => "Loaded blueprint \"{0}\"",
            "notification.region.copied" => "Copied {0} bricks ({1})",
            _ => value,
        };

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, System.Globalization.CultureInfo cultureInfo) => GetString(value);
    }
}
