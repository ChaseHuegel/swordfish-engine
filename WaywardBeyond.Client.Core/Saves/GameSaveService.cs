using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class GameSaveService(
    in ILogger<GameSaveService> logger,
    in IECSContext ecs,
    in ISerializer<VoxelEntityModel> voxelEntitySerializer,
    in NotificationService notificationService,
    in ILoadStage<GameOptions>[] createStages,
    in ILoadStage<GameSave>[] loadStages
) {
    private const string SAVES_FOLDER = "saves/";
    private const string GRIDS_FOLDER = "voxelEntities/";
    
    private readonly ILogger _logger = logger;
    private readonly IECSContext _ecs = ecs;
    private readonly ISerializer<VoxelEntityModel> _voxelEntitySerializer = voxelEntitySerializer;
    private readonly NotificationService _notificationService = notificationService;
    private readonly ILoadStage<GameOptions>[] _createStages = createStages;
    private readonly ILoadStage<GameSave>[] _loadStages = loadStages;

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
    
    public GameSave CreateSave(GameOptions options)
    {
        _notificationService.Push(new Notification($"Creating \"{options.Name}\"..."));
        
        PathInfo saveDirectory = _savesDirectory.At(options.Name);
        Directory.CreateDirectory(saveDirectory);
        
        byte[] seedBytes = Encoding.UTF8.GetBytes(options.Seed);
        byte[] seedHash = SHA1.HashData(seedBytes);
        var seed = BitConverter.ToInt32(seedHash);

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var level = new Level(WaywardBeyond.Version, seed, nowUtcMs, _AgeMs: 0, _SpawnX: 0, _SpawnY: 1, _SpawnZ: 5);
        var save = new GameSave(saveDirectory, options.Name, level);
        
        Save(save);
        
        _notificationService.Push(new Notification($"Created save \"{options.Name}\"."));
        return save;
    }
    
    //  TODO #325 loading a save should implicitly generate any missing data
    [Obsolete("This will be unified with Load in the future.")]
    public async Task GenerateSaveData(GameOptions options)
    {
        _notificationService.Push(new Notification($"Loading \"{options.Name}\"..."));
        
        for (var i = 0; i < _createStages.Length; i++)
        {
            ILoadStage<GameOptions> stage = _createStages[i];
            _currentStage = stage;
            await stage.Load(options);
        }

        _currentStage = null;
        _notificationService.Push(new Notification($"Loaded save \"{options.Name}\"."));
    }

    public async Task Load(GameSave save)
    {
        _notificationService.Push(new Notification($"Loading \"{save.Name}\"..."));
        
        for (var i = 0; i < _loadStages.Length; i++)
        {
            ILoadStage<GameSave> stage = _loadStages[i];
            _currentStage = stage;
            await stage.Load(save);
        }

        _currentStage = null;
        _notificationService.Push(new Notification($"Loaded save \"{save.Name}\"."));
    }
    
    public void Save(GameSave save)
    {
        _notificationService.Push(new Notification("Saving..."));
        
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
                
                PathInfo saveDirectory = save.Path.At(GRIDS_FOLDER);
                Directory.CreateDirectory(saveDirectory);
                
                PathInfo savePath = saveDirectory.At($"{guidComponent.Guid}.dat");
                savePath.Write(dataStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error saving voxel entity \"{entity}\".", entity);
                anyErrors = true;
            }
        }

        if (anyErrors)
        {
            _notificationService.Push(new Notification("Save failed, some information may be lost."));
        }
        else
        {
            _notificationService.Push(new Notification("Save complete."));
        }
    }
}