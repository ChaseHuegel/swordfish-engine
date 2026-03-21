using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Characters;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.UI.Layers;
using WaywardBeyond.Client.Core.Voxels.Models;

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
    in ILoadStage[] loadStages
) {
    private const string SAVES_FOLDER = "saves/";
    internal const string VOXEL_ENTITIES_SUBFOLDER = "voxelEntities/";
    internal const string CHARACTER_ENTITIES_SUBFOLDER = "characterEntities/";
    
    private readonly ILogger _logger = logger;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly IECSContext _ecs = ecs;
    private readonly ISerializer<VoxelEntityModel> _voxelEntitySerializer = voxelEntitySerializer;
    private readonly ISerializer<CharacterEntityModel> _characterEntitySerializer = characterEntitySerializer;
    private readonly NotificationService _notificationService = notificationService;
    private readonly ILoadStage<GameOptions>[] _newSaveStages = newSaveStages;
    private readonly ILoadStage<GameSave>[] _loadSaveStages = loadSaveStages;
    private readonly ILoadStage[] _loadStages = loadStages;

    private readonly PathInfo _savesDirectory = new(SAVES_FOLDER);
    
    private IProgressStage? _currentStage;
    
    public string GetStatus()
    {
        IProgressStage? stage = _currentStage;
        return stage != null ? stage.GetStatus() : "Complete";
    }
    
    public GameSave[] GetSaves()
    {
        PathInfo[] saveDirectories = _savesDirectory.GetFolders();
        var saves = new GameSave[saveDirectories.Length];
        
        for (var i = 0; i < saveDirectories.Length; i++)
        {
            PathInfo saveDirectory = saveDirectories[i];
            PathInfo levelFile = saveDirectory.At("level.dat");
            if (!levelFile.Exists())
            {
                continue;
            }
            
            byte[] levelData = levelFile.ReadBytes();
            Level level = Level.Deserialize(levelData);
            saves[i] = new GameSave(saveDirectory, saveDirectory.GetFileName(), level);
        }
        
        return saves;
    }
    
    public void CreateSave(GameOptions options)
    {
        _notificationService.Push(_localizedFormatter.GetString("notification.save.creating", options.Name));
        
        PathInfo saveDirectory = _savesDirectory.At(options.Name);
        Directory.CreateDirectory(saveDirectory);
        
        byte[] seedBytes = Encoding.UTF8.GetBytes(options.Seed);
        byte[] seedHash = SHA1.HashData(seedBytes);
        var seed = BitConverter.ToInt32(seedHash);

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var level = new Level(WaywardBeyond.Version, seed, nowUtcMs, _AgeMs: 0, _SpawnX: 0, _SpawnY: 1, _SpawnZ: 5);
        var save = new GameSave(saveDirectory, options.Name, level);
        
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
        if (!save.Path.At(VOXEL_ENTITIES_SUBFOLDER).DirectoryExists())
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
        
        //  Save the level.dat
        Directory.CreateDirectory(save.Path);
        PathInfo levelPath = save.Path.At("level.dat");
        Level level = save.Level;
        byte[] levelData = level.Serialize();
        using var levelDataStream = new MemoryStream(levelData);
        levelPath.Write(levelDataStream);

        //  Save voxel entities
        var anyErrors = false;
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
                using var dataStream = new MemoryStream(data);
                
                PathInfo saveDirectory = save.Path.At(VOXEL_ENTITIES_SUBFOLDER);
                Directory.CreateDirectory(saveDirectory);
                
                PathInfo savePath = saveDirectory.At($"{guidComponent.Guid}.dat");
                savePath.Write(dataStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error saving voxel entity \"{entity}\" ({guid}).", entity, guidComponent.Guid);
                anyErrors = true;
            }
        }
        
        //  Save character entities
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
                using var dataStream = new MemoryStream(data);
                
                PathInfo saveDirectory = save.Path.At(CHARACTER_ENTITIES_SUBFOLDER);
                Directory.CreateDirectory(saveDirectory);
                
                PathInfo savePath = saveDirectory.At($"{guidComponent.Guid}.dat");
                savePath.Write(dataStream);
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
}