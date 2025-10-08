using System;
using System.IO;
using System.Linq;
using System.Numerics;
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
            saves[i] = new GameSave(saveDirectory, saveDirectory.GetFileName());
        }
        
        return saves;
    }
    
    public GameSave CreateSave(string name)
    {
        PathInfo saveDirectory = _savesDirectory.At(name);
        Directory.CreateDirectory(saveDirectory);
        
        return new GameSave(saveDirectory, name);
    }

    public void Load(GameSave save)
    {
        _notificationService.Push(new Notification("Loading..."));

        PathInfo saveDirectory = save.Path.At(GRIDS_FOLDER);

        foreach (PathInfo gridFile in saveDirectory.GetFiles().OrderBy(pathInfo => pathInfo.OriginalString, new NaturalComparer()))
        {
            byte[] data = gridFile.ReadBytes();
            BrickEntityModel brickEntityModel = _brickEntitySerializer.Deserialize(data);
            
            _brickEntityBuilder.Create(gridFile.GetFileNameWithoutExtension(), brickEntityModel.Grid, brickEntityModel.Position, brickEntityModel.Orientation, Vector3.One);
        }
        
        _notificationService.Push(new Notification($"Loaded save \"{save.Name}\"."));
    }
    
    public void Save(GameSave save)
    {
        _notificationService.Push(new Notification("Saving..."));

        var anyErrors = false;

        _ecs.World.DataStore.Query<BrickComponent, TransformComponent>(0f, ForEachBrickEntity);
        void ForEachBrickEntity(float delta, DataStore store, int entity, ref BrickComponent brickComponent, ref TransformComponent transform)
        {
            try
            {
                var model = new BrickEntityModel(transform.Position, transform.Orientation, brickComponent.Grid);

                byte[] data = _brickEntitySerializer.Serialize(model);
                using var dataStream = new MemoryStream(data);

                PathInfo saveDirectory = save.Path.At(GRIDS_FOLDER);
                Directory.CreateDirectory(saveDirectory);
                
                PathInfo savePath = saveDirectory.At($"{entity}.dat");
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