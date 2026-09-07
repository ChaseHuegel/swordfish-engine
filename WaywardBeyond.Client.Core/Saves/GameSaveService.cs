using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.Saves.LoadGame;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class GameSaveService(
    in ILogger<GameSaveService> logger,
    in LocalizedFormatter localizedFormatter,
    in IECSContext ecs,
    in ISerializer<VoxelEntityModel> voxelEntitySerializer,
    in ISerializer<CharacterEntityModel> characterEntitySerializer,
    in NotificationService notificationService,
    in ILoadStage<GameOptions>[] newSaveStages,
    in ILoadStage<GameSave>[] loadSaveStages,
    in ILoadStage[] loadStages,
    in KeyValueStore keyValueStore,
    in WorldsClient worldsClient
) {
    private readonly ILogger _logger = logger;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly IECSContext _ecs = ecs;
    private readonly ISerializer<VoxelEntityModel> _voxelEntitySerializer = voxelEntitySerializer;
    private readonly ISerializer<CharacterEntityModel> _characterEntitySerializer = characterEntitySerializer;
    private readonly NotificationService _notificationService = notificationService;
    private readonly ILoadStage<GameOptions>[] _newSaveStages = newSaveStages;
    private readonly ILoadStage<GameSave>[] _loadSaveStages = loadSaveStages;
    private readonly ILoadStage[] _loadStages = loadStages;
    private readonly KeyValueStore _keyValueStore = keyValueStore;
    private readonly WorldsClient _worldsClient = worldsClient;

    private const string BUCKET_NAME = "levels";

    private readonly object _savesGate = new();
    private GameSave[] _saves = [];

    private IProgressStage? _currentStage;
    
    public string GetStatus()
    {
        IProgressStage? stage = _currentStage;
        return stage != null ? stage.GetStatus() : "Complete";
    }
    
    public GameSave[] GetSaves()
    {
        lock (_savesGate)
        {
            return _saves;
        }
    }

    /// <summary>
    /// Refreshes the cached save listing from the server-owned <c>levels</c> bucket (via a
    /// <see cref="ListWorldsRequest"/>). The client no longer owns world metadata; it maintains only a
    /// cached view for the menu.
    /// </summary>
    public async Task RefreshWorldsAsync()
    {
        try
        {
            Level[] levels = await _worldsClient.GetLevelsAsync();

            var saves = new GameSave[levels.Length];
            for (var i = 0; i < levels.Length; i++)
            {
                Level level = levels[i];
                saves[i] = new GameSave(level.Name, level);
            }

            lock (_savesGate)
            {
                _saves = saves;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh the world listing from the server.");
        }
    }

    public void CreateSave(GameOptions options)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.creating", options.Name));
        _ = CreateWorldAsync(options.Name, options.Seed);
    }

    /// <summary>
    /// Asks the server to flush its authoritative world to the <c>levels</c> bucket. The client no longer
    /// stores world state; quicksave/autosave/pause/close all delegate persistence to the server.
    /// </summary>
    public Task TriggerServerSave()
    {
        return _worldsClient.SaveWorldAsync();
    }

    private async Task CreateWorldAsync(string name, string seed)
    {
        bool success = await _worldsClient.CreateWorldAsync(name, seed, GameMode.Creative);
        await RefreshWorldsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.created" : "notification.save.creating.failed",
            name
        ));
    }

    public async Task Load(GameSave save)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.loading", save.Name));

        // Check if the save needs migration from disk to the KV
        bool existsInKv = false;
        Result<byte[]> checkResult = _keyValueStore.Get<byte[]>(BUCKET_NAME, save.Level.Guid);
        if (checkResult.Success && checkResult.Value.Length > 0)
        {
            existsInKv = true;
        }

        //  TODO remove this in a near-future update
        if (!existsInKv)
        {
            // Try migrating from disk
            string diskPath = Path.Combine("saves", save.Name);
            if (Directory.Exists(diskPath))
            {
                _notificationService.Push($"Migrating legacy save \"{save.Name}\"...");
                
                try
                {
                    // Migrate level
                    byte[] levelData = save.Level.Serialize();
                    _keyValueStore.Put(BUCKET_NAME, save.Level.Guid, levelData);

                    // Migrate voxel entities
                    string voxelEntitiesPath = Path.Combine(diskPath, "voxelEntities");
                    if (Directory.Exists(voxelEntitiesPath))
                    {
                        string[] files = Directory.GetFiles(voxelEntitiesPath);
                        foreach (string file in files)
                        {
                            try
                            {
                                byte[] data = await File.ReadAllBytesAsync(file);
                                VoxelEntityModel model = _voxelEntitySerializer.Deserialize(data);
                                _keyValueStore.Put(BUCKET_NAME, $"{save.Level.Guid}.entity.{model.Uuid}", data);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to migrate voxel entity from file \"{path}\"", file);
                            }
                        }
                    }

                    // Migrate character entities
                    string characterEntitiesPath = Path.Combine(diskPath, "characterEntities");
                    if (Directory.Exists(characterEntitiesPath))
                    {
                        string[] files = Directory.GetFiles(characterEntitiesPath);
                        foreach (string file in files)
                        {
                            try
                            {
                                byte[] data = await File.ReadAllBytesAsync(file);
                                CharacterEntityModel model = _characterEntitySerializer.Deserialize(data);
                                _keyValueStore.Put(BUCKET_NAME, $"{save.Level.Guid}.character.{model.Uuid}", data);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to migrate character entity from file \"{path}\"", file);
                            }
                        }
                    }

                    try
                    {
                        Directory.Delete(diskPath, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to clean up legacy save data \"{pasave.Name}\"", save.Name);
                    }

                    _notificationService.Push($"Migrated legacy save \"{save.Name}\"");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to migrate save \"{saveName}\" from disk.", save.Name);
                    _notificationService.Push($"Failed to migrate legacy save \"{save.Name}\"!");
                }
            }
        }
        
        for (var i = 0; i < _loadStages.Length; i++)
        {
            ILoadStage stage = _loadStages[i];
            _currentStage = stage;
            await stage.Load();
        }
        
        //  (re)generate world data if it doesn't exist
        bool voxelEntitiesExist = false;
        Result<string[]> keysResult = _keyValueStore.GetKeys(BUCKET_NAME);
        if (keysResult.Success)
        {
            string prefix = $"{save.Level.Guid}.entity.";
            for (var i = 0; i < keysResult.Value.Length; i++)
            {
                if (!keysResult.Value[i].StartsWith(prefix))
                {
                    continue;
                }
                
                voxelEntitiesExist = true;
                break;
            }
        }

        var generatedWorld = false;
        if (!voxelEntitiesExist)
        {
            var gameOptions = new GameOptions(save.Name, save.Level.Seed.ToString());
            for (var i = 0; i < _newSaveStages.Length; i++)
            {
                ILoadStage<GameOptions> stage = _newSaveStages[i];
                _currentStage = stage;
                await stage.Load(gameOptions);
            }

            generatedWorld = true;

            //  A freshly generated world only exists in the client store until the first autosave.
            //  Persist it now so the authoritative server can read it from the levels bucket when it
            //  processes this save's spawn request.
            PersistVoxelEntities(save.Level);
        }

        for (var i = 0; i < _loadSaveStages.Length; i++)
        {
            ILoadStage<GameSave> stage = _loadSaveStages[i];

            //  When the world was just generated above it is already present in the client store; skip
            //  the KV-backed voxel load stage, which would otherwise re-read the entries we just wrote
            //  and create duplicate entities.
            if (generatedWorld && stage is VoxelEntityLoadStage)
            {
                continue;
            }

            _currentStage = stage;
            await stage.Load(save);
        }

        _currentStage = null;
        _notificationService.Push(_localizedFormatter.GetString("notification.save.loaded", save.Name));
    }
    
    private void PersistVoxelEntities(Level level)
    {
        _ecs.World.DataStore.Query<VoxelComponent, TransformComponent>(0f, ForEachVoxelEntity);
        void ForEachVoxelEntity(float delta, DataStore store, int entity, in VoxelComponent voxelComponent, in TransformComponent transform)
        {
            try
            {
                Uuid uuid = store.GetUuid(entity);
                var model = new VoxelEntityModel(uuid, transform.Position, transform.Orientation, voxelComponent.VoxelObject);
                byte[] data = _voxelEntitySerializer.Serialize(model);

                _keyValueStore.Put(BUCKET_NAME, $"{level.Guid}.entity.{uuid}", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error persisting generated voxel entity \"{entity}\".", entity);
            }
        }
    }

    public void Delete(GameSave save)
    {
        _ = DeleteWorldAsync(save.Level.Guid, save.Name);
    }

    private async Task DeleteWorldAsync(string levelGuid, string name)
    {
        bool success = await _worldsClient.DeleteWorldAsync(levelGuid);
        await RefreshWorldsAsync();

        _notificationService.Push(_localizedFormatter.GetString(
            success ? "notification.save.deleted" : "notification.save.deleting.failed",
            name
        ));
    }
}
