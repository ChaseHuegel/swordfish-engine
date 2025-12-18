using System.Threading.Tasks;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Generation;
using WaywardBeyond.Client.Core.Voxels.Building;

namespace WaywardBeyond.Client.Core.Saves.NewGame;

internal sealed class WorldGenNewGameStage(
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
        return "Jumping to an asteroid belt";
    }
    
    public async Task Load(GameOptions options)
    {
        _progress = 0f;
        
        var worldGenerator = new WorldGenerator(options.Seed, _voxelEntityBuilder, _brickDatabase);
        await worldGenerator.Generate();
        
        _progress = 1f;
    }
}