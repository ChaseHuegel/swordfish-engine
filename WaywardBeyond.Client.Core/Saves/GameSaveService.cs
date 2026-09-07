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
    in KeyValueStore keyValueStore
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

    private const string BUCKET_NAME = "levels";
    
    private IProgressStage? _currentStage;
    
    public string GetStatus()
    {
        IProgressStage? stage = _currentStage;
        return stage != null ? stage.GetStatus() : "Complete";
    }
    
    public GameSave[] GetSaves()
    {
        Result<string[]> keysResult = _keyValueStore.GetKeys(BUCKET_NAME);
        var saves = new List<GameSave>();

        if (keysResult.Success)
        {
            var guidToKey = new Dictionary<string, string>();
            for (var i = 0; i < keysResult.Value.Length; i++)
            {
                string key = keysResult.Value[i];
                if (!Guid.TryParse(key, out _))
                {
                    continue;
                }
                
                guidToKey[key] = key;
            }

            foreach (KeyValuePair<string, string> kvp in guidToKey)
            {
                string key = kvp.Value;
                Result<byte[]> getResult = _keyValueStore.Get<byte[]>(BUCKET_NAME, key);
                if (!getResult.Success || getResult.Value.Length == 0)
                {
                    continue;
                }
                
                try
                {
                    Level level = Level.Deserialize(getResult.Value);
                    if (!string.IsNullOrEmpty(level.Guid))
                    {
                        var save = new GameSave(level.Name, level);
                        saves.Add(save);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize level from key \"{key}\"", key);
                }
            }
        }

        //  TODO remove this in a near-future update
        // Scan for old disk saves
        try
        {
            if (Directory.Exists("saves"))
            {
                string[] saveDirectories = Directory.GetDirectories("saves");
                for (var i = 0; i < saveDirectories.Length; i++)
                {
                    string dir = saveDirectories[i];
                    string levelFilePath = Path.Combine(dir, "level.dat");
                    if (!File.Exists(levelFilePath))
                    {
                        continue;
                    }
                    
                    try
                    {
                        byte[] levelBytes = File.ReadAllBytes(levelFilePath);
                        Level level = Level.Deserialize(levelBytes);
                        
                        //  level.Guid and level.Name didn't exist in the old format,
                        //  the folder name defined its uniqueness and name
                        level.Guid = Guid.NewGuid().ToString();
                        level.Name = Path.GetRelativePath("saves", dir);
                        
                        var save = new GameSave(level.Name, level);
                        saves.Add(save);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize legacy disk level from file \"{path}\"", levelFilePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan legacy disk saves directory.");
        }
        
        return saves.ToArray();
    }
    
    public void CreateSave(GameOptions options)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.creating", options.Name));
        
        byte[] seedBytes = Encoding.UTF8.GetBytes(options.Seed);
        byte[] seedHash = SHA1.HashData(seedBytes);
        var seed = BitConverter.ToInt32(seedHash);

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string guid = Guid.NewGuid().ToString();
        var level = new Level(
            WaywardBeyond.Version, 
            seed, 
            nowUtcMs, 
            _AgeMs: 0, 
            _SpawnX: 0, 
            _SpawnY: 1, 
            _SpawnZ: 5, 
            GameMode.Creative, 
            guid, 
            options.Name
        );
        var save = new GameSave(options.Name, level);
        
        Save(save);
        
        _notificationService.Push(_localizedFormatter.GetString("notification.save.created", options.Name));
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
    
    public void Save(GameSave save)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.saving", save.Name));
        
        Level level = save.Level;
        var anyErrors = false;

        //  Save level meta
        try
        {
            byte[] levelData = level.Serialize();
            _keyValueStore.Put(BUCKET_NAME, level.Guid, levelData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error saving level metadata.");
            anyErrors = true;
        }

        //  Save voxel entities
        _ecs.World.DataStore.Query<VoxelComponent, TransformComponent>(0f, ForEachVoxelEntity);
        void ForEachVoxelEntity(float delta, DataStore store, int entity, in VoxelComponent voxelComponent, in TransformComponent transform)
        {
            Uuid uuid = store.GetUuid(entity);

            try
            {
                var model = new VoxelEntityModel(uuid, transform.Position, transform.Orientation, voxelComponent.VoxelObject);
                byte[] data = _voxelEntitySerializer.Serialize(model);
                
                _keyValueStore.Put(BUCKET_NAME, $"{level.Guid}.entity.{uuid}", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error saving voxel entity \"{entity}\" ({uuid}).", entity, uuid);
                anyErrors = true;
            }
        }
        
        // Save character entities
        _ecs.World.DataStore.Query<CharacterComponent, TransformComponent>(0f, ForEachCharacterEntity);
        void ForEachCharacterEntity(float delta, DataStore store, int entity, in CharacterComponent characterComponent, in TransformComponent transform)
        {
            if (!store.TryGet(entity, out GameModeComponent gameModeComponent))
            {
                return;
            }

            Uuid uuid = store.GetUuid(entity);
            ulong characterId = characterComponent.Character.Id;

            try
            {
                var model = new CharacterEntityModel(uuid, transform.Position, transform.Orientation, gameModeComponent.GameMode);
                byte[] data = _characterEntitySerializer.Serialize(model);
                
                _keyValueStore.Put(BUCKET_NAME, $"{level.Guid}.character.{characterId}", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error saving character entity \"{entity}\" ({uuid}).", entity, uuid);
                anyErrors = true;
            }
        }

        if (anyErrors)
        {
            _notificationService.Push(_localizedFormatter.GetString("notification.save.saving.failed", save.Name));
        }
        else
        {
            _notificationService.Push(_localizedFormatter.GetString("notification.save.saved", save.Name));
        }
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
        try
        {
            _keyValueStore.Delete(BUCKET_NAME, save.Level.Guid);

            Result<string[]> keysResult = _keyValueStore.GetKeys(BUCKET_NAME);
            if (keysResult.Success)
            {
                string prefix = $"{save.Level.Guid}.";
                var keysToDelete = new List<string>();
                for (var i = 0; i < keysResult.Value.Length; i++)
                {
                    if (!keysResult.Value[i].StartsWith(prefix))
                    {
                        continue;
                    }
                    
                    keysToDelete.Add(keysResult.Value[i]);
                }
                    
                if (keysToDelete.Count > 0)
                {
                    _keyValueStore.Delete(BUCKET_NAME, keysToDelete.ToArray());
                }
            }

            //  TODO remove this in a near-future update
            //  Delete the legacy disk-based directory, if it exists
            string diskPath = Path.Combine("saves", save.Name);
            if (Directory.Exists(diskPath))
            {
                Directory.Delete(diskPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete save \"{saveName}\" ({guid}).", save.Name, save.Level.Guid);
        }
    }
}
