using System;
using System.Collections.Generic;
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
        if (!keysResult.Success)
        {
            return [];
        }

        var saves = new List<GameSave>();
        for (var i = 0; i < keysResult.Value.Length; i++)
        {
            string key = keysResult.Value[i];
            if (!key.EndsWith(".meta"))
            {
                continue;
            }
            
            Result<byte[]> getResult = _keyValueStore.Get<byte[]>(BUCKET_NAME, key);
            if (!getResult.Success)
            {
                continue;
            }
            
            try
            {
                Level level = Level.Deserialize(getResult.Value);
                var save = new GameSave(level.Name, level);
                saves.Add(save);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize level from key \"{key}\"", key);
            }
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

        if (!voxelEntitiesExist)
        {
            var gameOptions = new GameOptions(save.Name, save.Level.Seed.ToString());
            for (var i = 0; i < _newSaveStages.Length; i++)
            {
                ILoadStage<GameOptions> stage = _newSaveStages[i];
                _currentStage = stage;
                await stage.Load(gameOptions);
            }
        }

        for (var i = 0; i < _loadSaveStages.Length; i++)
        {
            ILoadStage<GameSave> stage = _loadSaveStages[i];
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
            _keyValueStore.Put(BUCKET_NAME, $"{level.Guid}.meta", levelData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an error saving level metadata.");
            anyErrors = true;
        }

        //  Save version, this is allowed to fail because it isn't a source of truth.
        //  This is a human-readable value for troubleshooting without deserialization
        try
        {
            string versionString = $"{level.Version.Environment}_{level.Version.Name}_{level.Version.DataVersion})";
            _keyValueStore.Put(BUCKET_NAME, $"{level.Guid}.version", versionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "There was an error saving level version reference.");
        }

        //  Save voxel entities
        _ecs.World.DataStore.Query<VoxelComponent, TransformComponent>(0f, ForEachVoxelEntity);
        void ForEachVoxelEntity(float delta, DataStore store, int entity, ref VoxelComponent voxelComponent, ref TransformComponent transform)
        {
            if (!store.TryGet(entity, out GuidComponent guidComponent))
            {
                return;
            }
            
            try
            {
                var model = new VoxelEntityModel(guidComponent.Guid, transform.Position, transform.Orientation, voxelComponent.VoxelObject);
                byte[] data = _voxelEntitySerializer.Serialize(model);
                
                _keyValueStore.Put(BUCKET_NAME, $"{level.Guid}.entity.{guidComponent.Guid}", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error saving voxel entity \"{entity}\" ({guid}).", entity, guidComponent.Guid);
                anyErrors = true;
            }
        }
        
        // Save character entities
        _ecs.World.DataStore.Query<CharacterComponent, TransformComponent>(0f, ForEachCharacterEntity);
        void ForEachCharacterEntity(float delta, DataStore store, int entity, ref CharacterComponent characterComponent, ref TransformComponent transform)
        {
            if (!store.TryGet(entity, out GuidComponent guidComponent))
            {
                return;
            }
            
            if (!store.TryGet(entity, out GameModeComponent gameModeComponent))
            {
                return;
            }
            
            try
            {
                var model = new CharacterEntityModel(guidComponent.Guid, transform.Position, transform.Orientation, gameModeComponent.GameMode);
                byte[] data = _characterEntitySerializer.Serialize(model);
                
                _keyValueStore.Put(BUCKET_NAME, $"{level.Guid}.character.{guidComponent.Guid}", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error saving character entity \"{entity}\" ({guid}).", entity, guidComponent.Guid);
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

    public void Delete(GameSave save)
    {
        try
        {
            _keyValueStore.Delete(BUCKET_NAME, $"{save.Level.Guid}.meta");
            _keyValueStore.Delete(BUCKET_NAME, $"{save.Level.Guid}.version");
            
            Result<string[]> keysResult = _keyValueStore.GetKeys(BUCKET_NAME);
            if (!keysResult.Success)
            {
                return;
            }
            
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete save \"{saveName}\" ({guid}).", save.Name, save.Level.Guid);
        }
    }
}
