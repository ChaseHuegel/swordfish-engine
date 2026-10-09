using System;
using DryIoc;
using Shoal.DependencyInjection;
using Shoal.CommandLine;
using Shoal.Extensions.Swordfish;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Identity;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Meta;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Serialization;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Layers.Menus.Pause;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Server;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Commands;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;

namespace WaywardBeyond.Client;

// ReSharper disable once UnusedType.Global
public class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        RegisterConfiguration(container);
        RegisterUI(container);
        RegisterInput(container);
        RegisterDatabases(container);
        RegisterParsers(container);
        RegisterEntitySystems(container);
        RegisterVoxels(container);
        RegisterWebhooks(container);
        RegisterShortcuts(container);
        RegisterEvents(container);
        RegisterNetworking(container);
        
        container.Register<PlayerData>(Reuse.Singleton);
        
        container.Register<LocalizedFormatter>(Reuse.Singleton);
        
        container.Register<ExternalAppService>(Reuse.Singleton);
        
        container.Register<SoundEffectService>(Reuse.Singleton);
        
        container.Register<CharacterAssetService>(Reuse.Singleton);
        
        container.Register<NotificationService>(Reuse.Singleton);
        
        container.RegisterDelegate(factory: () => new Randomizer());
        container.Register<NameGenerator>(Reuse.Singleton);
        
        container.Register<MusicSystem>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, MusicSystem>();
        container.RegisterMapping<IDebugOverlay, MusicSystem>();
        
        container.Register<CharacterSaveManager>(Reuse.Singleton);
        container.Register<ActiveCharacterSave>(Reuse.Singleton);
        container.Register<ICharacterStorage, SqliteCharacterStorage>(Reuse.Singleton);
        container.Register<ISaveMetaStorage, SqliteSaveMetaStorage>(Reuse.Singleton);
        
        container.Register<PlayerCharacterEntityBuilder>(Reuse.Transient);
        
        container.Register<GameSaveService>(Reuse.Singleton);
        container.Register<GameSaveManager>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, GameSaveManager>();

        container.Register<IUserClaimProvider, ProfileUserClaimProvider>(Reuse.Singleton);
        
        container.Register<Entry>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, Entry>();
    }

    private void RegisterEvents(IContainer container)
    {
        container.Register<EventInvoker<PlaceEvent>>();
        container.Register<EventInvoker<BreakEvent>>();
        container.Register<EventInvoker<PlayerMovedEvent>>();
    }

    private static void RegisterNetworking(IContainer container)
    {
        //  The shared host wire-up (registry, serializers, loopback transports, hub, save storage,
        //  networking config) lives in Server.Core so the embedded host and the dedicated
        //  launcher compose from the same source.
        HostComposition.RegisterNetworking(container, seedLocalLoopback: true);

        container.Register<TransportManager>(Reuse.Singleton);
        container.RegisterDelegate<IClientConnection>(context =>
        {
            TransportManager transport = context.Resolve<TransportManager>();
            if (NetworkModeResolver.Resolve(context.Resolve<CommandLineArgs>()) == NetworkMode.Host)
            {
                transport.UseLocal(context.Resolve<LocalConnection>().Client);
            }
            return transport;
        }, Reuse.Singleton);
        container.Register<GameClient>(Reuse.Singleton);
        container.Register<SnapshotAckTracker>(Reuse.Singleton);
        container.Register<ServerStats>(Reuse.Singleton);

        container.Register<IEntitySystem, ClientHeartbeatSystem>();

        container.Register<IEntitySystem, ClientInputSystem>();
        container.Register<IEntitySystem, ClientReplicationSystem>();
        container.Register<IEntitySystem, ClientReconcileSystem>();
        container.Register<IEntitySystem, ClientVoxelReconcileSystem>();
        container.Register<ChatService>(Reuse.Singleton);
        container.Register<IEntitySystem, ClientChatSystem>();
        //  Runs after the voxel-edit producers (PlayerInteractionService, ClientVoxelReconcileSystem) so
        //  the dirty VoxelComponent flags it consumes reflect the current prediction/reconcile.
        container.Register<IEntitySystem, VoxelEntityRebuildSystem>();

        container.Register<ClientJoinSystem>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, ClientJoinSystem>();
        container.Register<ClientCleanupSystem>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, ClientCleanupSystem>();

        container.Register<ClientDisconnectSystem>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, ClientDisconnectSystem>();

        container.Register<LevelsClient>(Reuse.Singleton);
        container.Register<IEntitySystem, ClientLevelServiceSystem>();
        container.Register<ClientNotificationSystem>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, ClientNotificationSystem>();
        container.Register<LanDiscoveryService>(Reuse.Singleton);
    }

    private void RegisterShortcuts(IContainer container)
    {
        container.Register<IAutoActivate, ShortcutRegistrar>();
        container.Register<ShortcutRegistration, ScreenshotShortcut>();
    }

    private static void RegisterWebhooks(IContainer container)
    {
        var feedbackSourceUri = new Uri("https://gist.githubusercontent.com/ChaseHuegel/ed8b4d594789c4567e352148e006dbc1/raw/wb-feedback-webhook.txt");
        var discordUri = new Uri("https://gist.githubusercontent.com/ChaseHuegel/fce325f4a4d41684277994eaa730dc5b/raw/wb-discord");
        var steamUri = new Uri("https://gist.githubusercontent.com/ChaseHuegel/c3b57b884e2c675fcbffdba869d2f05d/raw/wb-steam");
        var webhooks = new Webhooks(feedbackSourceUri, discordUri, steamUri);
        container.RegisterInstance(webhooks);
        
        container.Register<WebhookService>();
        container.Register<FeedbackWebhook>();
    }

    private static void RegisterConfiguration(IContainer container)
    {
        container.Register<SettingsManager>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, SettingsManager>();
        
        container.RegisterConfig<ControlSettings>(file: "control.toml");
        container.RegisterConfig<VolumeSettings>(file: "volume.toml");
        container.RegisterConfig<DebugSettings>(file: "debug.toml");
        container.RegisterConfig<UISettings>(file: "ui.toml");
        container.RegisterConfig<GameplaySettings>(file: "gameplay.toml");
        container.RegisterConfig<NetworkingSettings>(file: "network.toml");
        container.RegisterConfig<ProfileSettings>(file: "profile.toml");
        container.RegisterConfig<ChatSettings>(file: "chat.toml");
    }

    private static void RegisterUI(IContainer container)
    {
        container.Register<DefaultUIRenderer>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, DefaultUIRenderer>();
        
        container.Register<IUILayer, CrosshairOverlay>(Reuse.Singleton);
        
        container.Register<ShapeSelector>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ShapeSelector>();
        container.RegisterMapping<IActionIndicator, ShapeSelector>();

        container.Register<OrientationSelector>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, OrientationSelector>();
        container.RegisterMapping<IActionIndicator, OrientationSelector>();

        //  Nameplates are world-correlated UI: ordered below the HUD layers (hotbar, inventory,
        //  notifications) so player tags render behind the widgets.
        container.Register<NameplateSnapshot>(Reuse.Singleton);
        container.Register<NameplateUILayer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, NameplateUILayer>();

        container.Register<Hotbar>(Reuse.Singleton);
        container.Register<Actions>(Reuse.Singleton);
        container.Register<Bars>(Reuse.Singleton);
        
        container.Register<HUD>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, HUD>();

        container.Register<MainMenu>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, MainMenu>();
        container.Register<IMenuPage<MenuPage>, UI.Layers.Menus.Main.HomePage>();
        container.Register<IMenuPage<MenuPage>, MainMenuSettingsPage>();
        container.Register<IMenuPage<MenuPage>, SelectSavePage>();
        container.Register<IMenuPage<MenuPage>, NewSavePage>();
        container.Register<IMenuPage<MenuPage>, CharactersPage>();
        container.Register<IMenuPage<MenuPage>, SelectCharacterPage>();
        container.Register<IMenuPage<MenuPage>, NewCharacterPage>();
        container.Register<IMenuPage<MenuPage>, MultiplayerPage>();
        
        container.Register<PauseMenu>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, PauseMenu>();
        container.Register<IMenuPage<PausePage>, UI.Layers.Menus.Pause.HomePage>();
        container.Register<IMenuPage<PausePage>, PauseMenuSettingsPage>();
        
        container.Register<IUILayer, LoadScreen>(Reuse.Singleton);
        container.Register<IUILayer, VersionWatermark>(Reuse.Singleton);
        
        container.Register<Inventory>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, Inventory>();
        
        container.Register<ControlHints>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ControlHints>();
        
        container.Register<ToastNotificationLayer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ToastNotificationLayer>();
                
        container.Register<InteractionNotificationLayer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, InteractionNotificationLayer>();
        
        container.Register<DebugOverlayRenderer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, DebugOverlayRenderer>();
        container.Register<IDebugOverlay, PerformanceStatsOverlay>();
        container.Register<IDebugOverlay, NetworkStatsOverlay>();
        
        container.Register<ChatLayer>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ChatLayer>();

        container.Register<ModalMenu>(Reuse.Singleton);
        container.RegisterMapping<IUILayer, ModalMenu>();
        container.Register<IMenuPage<Modal>, EmptyModal>();
        container.Register<IMenuPage<Modal>, FeedbackModal>();
        container.Register<IMenuPage<Modal>, PlaytestNoticeModal>();
        container.Register<ConfirmModal>(Reuse.Singleton);
        container.RegisterMapping<IMenuPage<Modal>, ConfirmModal>();
    }
    
    private static void RegisterInput(IContainer container)
    {
        container.Register<InteractionState>(Reuse.Singleton);
        
        container.Register<PlayerInteractionService>(Reuse.Singleton);
        container.RegisterMapping<IEntryPoint, PlayerInteractionService>();
        container.RegisterMapping<IEntitySystem, PlayerInteractionService>();
        container.RegisterMapping<IDebugOverlay, PlayerInteractionService>();

        container.Register<ClientPlayerMotionProcessor>(Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, ClientPlayerMotionProcessor>();
    }
    
    private static void RegisterDatabases(IContainer container)
    {
        container.Register<ItemDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<Item>, ItemDatabase>();
        
        //  Shared interaction-content resolution used by the in-process server to author placeable/loot.
        container.Register<IInteractionContent, ClientInteractionContent>(Reuse.Singleton);
        
        container.Register<LocalizedTagsDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<LocalizedTags>, LocalizedTagsDatabase>();
        
        container.Register<BlueprintDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<VoxelEntityModel>, BlueprintDatabase>();
    }
    
    private static void RegisterParsers(IContainer container)
    {
        container.RegisterTomlParser<ItemDefinitions>();
        
        container.RegisterMany<PBRTextureArraysParser>(reuse: Reuse.Singleton);
        container.RegisterMany<VoxelEntityModelParser>();
        container.RegisterMany<LocalizedTagDefinitionParser>();
    }

    private static void RegisterEntitySystems(IContainer container)
    {
        container.Register<IEntitySystem, PlayerViewModelSystem>();
        container.Register<IEntitySystem, CleanupMeshRendererSystem>();
        container.Register<IEntitySystem, MainMenuAnimationSystem>();
        container.Register<IEntitySystem, ActiveSlotNotificationSystem>();

        container.Register<IEntitySystem, RemotePlayerVisualSystem>();
        container.Register<IEntitySystem, BillboardSystem>();
        container.Register<IEntitySystem, NameplateSystem>();

        container.Register<AudioChannelSystem>(reuse: Reuse.Singleton);
        container.RegisterMapping<IEntitySystem, AudioChannelSystem>();
    }
    
    private static void RegisterVoxels(IContainer container)
    {
        //  TODO this was thrown together for testing and needs cleaned up
        container.RegisterDelegate<Shader>(context => context.Resolve<IFileParseService>().Parse<Shader>(AssetPaths.Shaders.At("lightedArray.glsl")), Reuse.Singleton);
        container.RegisterDelegate<TextureArray>(context => context.Resolve<IFileParseService>().Parse<TextureArray>(AssetPaths.Textures.At("block\\")), Reuse.Singleton);
        container.RegisterDelegate<PBRTextureArrays>(context => context.Resolve<IFileParseService>().Parse<PBRTextureArrays>(AssetPaths.Textures.At("block\\")), Reuse.Singleton);
        container.RegisterDelegate<DataStore>(context => context.Resolve<IECSContext>().World.DataStore, Reuse.Singleton);
        
        container.Register<VoxelEntityBuilder>(Reuse.Transient);
        container.Register<IVoxelEntityDecorator, LightEntityDecorator>(Reuse.Transient);
        container.Register<IVoxelEntityDecorator, ThrusterEntityDecorator>(Reuse.Transient);
        
        container.Register<VoxelObjectBuilder>(Reuse.Singleton);
        
        container.Register<VoxelObjectProcessor>(Reuse.Scoped);
        
        container.Register<DepthState>(Reuse.Scoped);
        container.Register<LightingState>(Reuse.Scoped);
        container.Register<MeshState>(Reuse.Scoped);
        container.Register<CollisionState>(Reuse.Scoped);
        container.Register<EntityState>(Reuse.Scoped);
        
        container.Register<VoxelObjectProcessor.IPass, AmbientLightPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.IPass, LightPropagationPass>(Reuse.Scoped);
        
        container.Register<VoxelObjectProcessor.IVoxelPass, LightSeedPrePass>(Reuse.Scoped);
        
        container.Register<VoxelObjectProcessor.ISamplePass, DepthPrePass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, LightPropagationPrePass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, MeshPostPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, CollisionPostPass>(Reuse.Scoped);
        container.Register<VoxelObjectProcessor.ISamplePass, VoxelEntityPostPass>(Reuse.Scoped);
    }
}