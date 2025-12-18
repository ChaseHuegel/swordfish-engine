using System;
using System.Numerics;
using System.Threading.Tasks;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Building;

namespace WaywardBeyond.Client.Core.Saves.NewGame;

internal sealed class StarterShipNewGameStage(
    in VoxelEntityBuilder voxelEntityBuilder,
    in BrickDatabase brickDatabase
) : ILoadStage<GameOptions>
{
    private readonly VoxelEntityBuilder _voxelEntityBuilder = voxelEntityBuilder;
    private readonly BrickDatabase _brickDatabase = brickDatabase;
    
    private float _progress;
    
    public float GetProgress()
    {
        return _progress;
    }

    public string GetStatus()
    {
        return "Crashing your ship";
    }
    
    public Task Load(GameOptions options)
    {
        _progress = 0f;
        
        var shipVoxelObject = new VoxelObject(chunkSize: 16);
        shipVoxelObject.Set(0, 0, 0, _brickDatabase.Get("ship_core").Value.ToVoxel());
        _voxelEntityBuilder.Create(Guid.NewGuid(), shipVoxelObject, Vector3.Zero, Quaternion.Identity, Vector3.One);
        
        _progress = 1f;
        return Task.CompletedTask;
    }
}