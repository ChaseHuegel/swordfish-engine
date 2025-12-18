using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Saves.LoadGame;

internal sealed class VoxelEntityLoadStage(
    in ISerializer<VoxelEntityModel> voxelEntitySerializer,
    in VoxelEntityBuilder voxelEntityBuilder
) : ILoadStage<GameSave>
{
    private const string VOXEL_ENTITIES_FOLDER = "voxelEntities/";
    
    private readonly ISerializer<VoxelEntityModel> _voxelEntitySerializer = voxelEntitySerializer;
    private readonly VoxelEntityBuilder _voxelEntityBuilder = voxelEntityBuilder;

    private float _progress;
    
    public float GetProgress()
    {
        return _progress;
    }

    public string GetStatus()
    {
        return "Waking you from cryosleep";
    }
    
    public Task Load(GameSave save)
    {
        _progress = 0f;
        PathInfo[] voxelEntityFiles = save.Path.At(VOXEL_ENTITIES_FOLDER).GetFiles();

        var processedFiles = 0;
        foreach (PathInfo voxelEntityFile in voxelEntityFiles.OrderBy(pathInfo => pathInfo.OriginalString, new NaturalComparer()))
        {
            byte[] data = voxelEntityFile.ReadBytes();
            VoxelEntityModel voxelEntityModel = _voxelEntitySerializer.Deserialize(data);
            
            _voxelEntityBuilder.Create(voxelEntityModel.Guid, voxelEntityModel.VoxelObject, voxelEntityModel.Position, voxelEntityModel.Orientation, Vector3.One);

            processedFiles++;
            _progress = 1f / voxelEntityFiles.Length * processedFiles;
        }

        return Task.CompletedTask;
    }
}