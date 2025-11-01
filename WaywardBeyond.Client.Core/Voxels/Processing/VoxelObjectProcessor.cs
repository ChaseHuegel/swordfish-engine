using System.Collections.Generic;

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
    
    public void Process(VoxelObject voxelObject)
    {
        PrePass(voxelObject);
        MainPass(voxelObject);
        PostPass(voxelObject);
    }

    private void MainPass(VoxelObject voxelObject)
    {
        for (var i = 0; i < _passes.Length; i++)
        {
            _passes[i].Process(voxelObject);
        }
    }

    private void PrePass(VoxelObject voxelObject)
    {
        //  Run voxel pre-pass
        if (_voxelPasses.TryGetValue(Stage.PrePass, out List<IVoxelPass>? voxelPasses))
        {
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
            foreach (VoxelSample sample in voxelObject.GetSampler())
            {
                for (var i = 0; i < samplePasses.Count; i++)
                {
                    samplePasses[i].Process(sample);
                }
            }
        }
    }
    
    private void PostPass(VoxelObject voxelObject)
    {
        //  Run voxel post-pass
        if (_voxelPasses.TryGetValue(Stage.PostPass, out List<IVoxelPass>? voxelPasses))
        {
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
            foreach (VoxelSample sample in voxelObject.GetSampler())
            {
                for (var i = 0; i < samplePasses.Count; i++)
                {
                    samplePasses[i].Process(sample);
                }
            }
        }
    }
    
    public interface IPass
    {
        void Process(VoxelObject voxelObject);
    }
    
    public interface IVoxelPass
    {
        Stage Stage { get; }
        
        void Process(ref Voxel voxel);
    }

    public interface ISamplePass
    {
        Stage Stage { get; }
        
        void Process(VoxelSample sample);
    }

    public enum Stage
    {
        PrePass,
        PostPass,
    }
}