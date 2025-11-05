using System.Collections.Generic;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class VoxelObjectProcessor
{
    private readonly IPass[] _passes;
    private readonly Dictionary<Stage, List<IVoxelPass>> _voxelPasses;
    private readonly Dictionary<Stage, List<ISamplePass>> _samplePasses;

    public VoxelObjectProcessor(IPass[] passes, IVoxelPass[] voxelPasses, ISamplePass[] samplePasses)
    {
        _passes = passes;
        
        _voxelPasses = [];
        foreach (IVoxelPass voxelPass in voxelPasses)
        {
            if (!_voxelPasses.TryGetValue(voxelPass.Stage, out List<IVoxelPass>? stagePasses))
            {
                stagePasses = [];
                _voxelPasses[voxelPass.Stage] = stagePasses;
            }
            
            stagePasses.Add(voxelPass);
        }

        _samplePasses = [];
        foreach (ISamplePass samplePass in samplePasses)
        {
            if (!_samplePasses.TryGetValue(samplePass.Stage, out List<ISamplePass>? stagePasses))
            {
                stagePasses = [];
                _samplePasses[samplePass.Stage] = stagePasses;
            }
            
            stagePasses.Add(samplePass);
        }
    }
    
    /// <summary>
    ///     Processes a <see cref="VoxelObject"/>, generating all data to build its representation(s).
    /// </summary>
    /// <returns>The number of passes over the <see cref="VoxelObject"/>.</returns>
    public int Process(VoxelObject voxelObject)
    {
        var passes = 0;
        passes += PrePass(voxelObject);
        passes += MainPass(voxelObject);
        passes += PostPass(voxelObject);
        return passes;
    }

    private int MainPass(VoxelObject voxelObject)
    {
        var passes = 0;
        for (var i = 0; i < _passes.Length; i++)
        {
            _passes[i].Process(voxelObject);
            passes++;
        }

        return passes;
    }

    private int PrePass(VoxelObject voxelObject)
    {
        var passes = 0;
        
        //  Run voxel pre-pass
        if (_voxelPasses.TryGetValue(Stage.PrePass, out List<IVoxelPass>? voxelPasses))
        {
            passes++;
            foreach (ref Voxel voxel in voxelObject)
            {
                for (var i = 0; i < voxelPasses.Count; i++)
                {
                    voxelPasses[i].Process(ref voxel);
                }
            }
        }
        
        //  Run sample pre-pass
        if (_samplePasses.TryGetValue(Stage.PrePass, out List<ISamplePass>? samplePasses))
        {
            passes++;
            foreach (VoxelSample sample in voxelObject.GetSampler())
            {
                for (var i = 0; i < samplePasses.Count; i++)
                {
                    samplePasses[i].Process(sample);
                }
            }
        }
        
        return passes;
    }
    
    private int PostPass(VoxelObject voxelObject)
    {
        var passes = 0;
        
        //  Run voxel post-pass
        if (_voxelPasses.TryGetValue(Stage.PostPass, out List<IVoxelPass>? voxelPasses))
        {
            passes++;
            foreach (ref Voxel voxel in voxelObject)
            {
                for (var i = 0; i < voxelPasses.Count; i++)
                {
                    voxelPasses[i].Process(ref voxel);
                }
            }
        }

        //  Run sample post-pass
        if (_samplePasses.TryGetValue(Stage.PostPass, out List<ISamplePass>? samplePasses))
        {
            passes++;
            foreach (VoxelSample sample in voxelObject.GetSampler())
            {
                for (var i = 0; i < samplePasses.Count; i++)
                {
                    samplePasses[i].Process(sample);
                }
            }
        }
        
        return passes;
    }
    
    public interface IPass
    {
        void Process(VoxelObject voxelObject);
    }
    
    public interface IVoxelPass
    {
        Stage Stage { get; }

        //  TODO implement usages
        bool ShouldProcessChunk(ChunkData chunkData);
        
        void Process(ref Voxel voxel);
    }

    public interface ISamplePass
    {
        Stage Stage { get; }

        //  TODO implement usages
        bool ShouldProcessChunk(ChunkData chunkData);

        void Process(VoxelSample sample);
    }

    public enum Stage
    {
        PrePass,
        PostPass,
    }
}