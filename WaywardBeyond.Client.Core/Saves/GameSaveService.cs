using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;

namespace WaywardBeyond.Client.Core.Saves;

internal sealed class GameSaveService(
    in ILogger<GameSaveService> logger,
    in IECSContext ecs,
    in ISerializer<BrickEntityModel> brickEntitySerializer,
    in BrickEntityBuilder brickEntityBuilder,
    in NotificationService notificationService
) {
    private const string SAVES_FOLDER = "saves/";
    private const string GRIDS_FOLDER = "grids/";
    
    private readonly ILogger _logger = logger;
    private readonly IECSContext _ecs = ecs;
    private readonly ISerializer<BrickEntityModel> _brickEntitySerializer = brickEntitySerializer;
    private readonly BrickEntityBuilder _brickEntityBuilder = brickEntityBuilder;
    private readonly NotificationService _notificationService = notificationService;

    private readonly PathInfo _savesDirectory = new(SAVES_FOLDER);

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
        PathInfo saveDirectory = _savesDirectory.At(options.Name);
        Directory.CreateDirectory(saveDirectory);
        
        byte[] seedBytes = Encoding.UTF8.GetBytes(options.Seed);
        byte[] seedHash = SHA1.HashData(seedBytes);
        var seed = BitConverter.ToInt32(seedHash);

        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var level = new Level(WaywardBeyond.Version, seed, nowUtcMs, _AgeMs: 0, _SpawnX: 0, _SpawnY: 1, _SpawnZ: 5);
        var save = new GameSave(saveDirectory, options.Name, level);
        
        Save(save);
        return save;
    }

    public void Load(GameSave save)
    {
        _notificationService.Push(new Notification("Loading..."));

        PathInfo saveDirectory = save.Path.At(GRIDS_FOLDER);
        foreach (PathInfo gridFile in saveDirectory.GetFiles().OrderBy(pathInfo => pathInfo.OriginalString, new NaturalComparer()))
        {
            byte[] data = gridFile.ReadBytes();
            BrickEntityModel brickEntityModel = _brickEntitySerializer.Deserialize(data);
            
            _brickEntityBuilder.Create(brickEntityModel.Guid, brickEntityModel.Grid, brickEntityModel.Position, brickEntityModel.Orientation, Vector3.One);
        }
        
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

        //  Save brick entities
        var anyErrors = false;
        _ecs.World.DataStore.Query<BrickComponent, TransformComponent>(0f, ForEachBrickEntity);
        void ForEachBrickEntity(float delta, DataStore store, int entity, ref BrickComponent brickComponent, ref TransformComponent transform)
        {
            if (!store.TryGet(entity, out GuidComponent guidComponent))
            {
                return;
            }
            
            try
            {
                var model = new BrickEntityModel(guidComponent.Guid, transform.Position, transform.Orientation, brickComponent.Grid);
                
                byte[] data = _brickEntitySerializer.Serialize(model);
                using var dataStream = new MemoryStream(data);
                
                PathInfo saveDirectory = save.Path.At(GRIDS_FOLDER);
                Directory.CreateDirectory(saveDirectory);
                
                PathInfo savePath = saveDirectory.At($"{guidComponent.Guid}.dat");
                savePath.Write(dataStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error saving brick entity \"{entity}\".", entity);
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