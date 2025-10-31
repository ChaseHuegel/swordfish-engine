using System.Collections.Generic;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

public class VoxelObjectLightSeedPass : IVoxelSampleProcessor
{
    public VoxelProcessorPass Pass => VoxelProcessorPass.PrePass;
    
    public void Process(VoxelSample sample)
    {
    }
}

public class VoxelObjectLightPropagationPass : IVoxelSampleProcessor
{
    public VoxelProcessorPass Pass => VoxelProcessorPass.PrePass;
    
    public void Process(VoxelSample sample)
    {
    }
}

public class VoxelObjectMeshPass : IVoxelSampleProcessor
{
    public VoxelProcessorPass Pass => VoxelProcessorPass.PostPass;
    
    public void Process(VoxelSample sample)
    {
    }
}

public class VoxelObjectTexturePass : IVoxelSampleProcessor
{
    public VoxelProcessorPass Pass => VoxelProcessorPass.PostPass;
    
    public void Process(VoxelSample sample)
    {
    }
}

public interface IVoxelProcessor
{
    VoxelProcessorPass Pass { get; }
    
    void Process(ref Voxel voxel);
}

public interface IVoxelSampleProcessor
{
    VoxelProcessorPass Pass { get; }
    
    void Process(VoxelSample sample);
}

public enum VoxelProcessorPass
{
    PrePass,
    PostPass
}

public class VoxelObjectProcessor
{
    private readonly Dictionary<VoxelProcessorPass, List<IVoxelProcessor>> _voxelProcessors;
    private readonly Dictionary<VoxelProcessorPass, List<IVoxelSampleProcessor>> _sampleProcessors;

    public VoxelObjectProcessor(IVoxelProcessor[] voxelProcessors, IVoxelSampleProcessor[] sampleProcessors)
    {
        _voxelProcessors = [];
        foreach (IVoxelProcessor voxelProcessor in voxelProcessors)
        {
            if (!_voxelProcessors.TryGetValue(voxelProcessor.Pass, out List<IVoxelProcessor>? processors))
            {
                processors = [];
                _voxelProcessors[voxelProcessor.Pass] = processors;
            }
            
            processors.Add(voxelProcessor);
        }

        _sampleProcessors = [];
        foreach (IVoxelSampleProcessor sampleProcessor in sampleProcessors)
        {
            if (!_sampleProcessors.TryGetValue(sampleProcessor.Pass, out List<IVoxelSampleProcessor>? processors))
            {
                processors = [];
                _sampleProcessors[sampleProcessor.Pass] = processors;
            }
            
            processors.Add(sampleProcessor);
        }
    }
    
    public void Process(VoxelObject voxelObject)
    {
        ProcessVoxels(voxelObject);
        SampleVoxels(voxelObject);
    }

    private void ProcessVoxels(VoxelObject voxelObject)
    {
        if (_voxelProcessors.TryGetValue(VoxelProcessorPass.PrePass, out List<IVoxelProcessor>? processors))
        {
            foreach (ref Voxel voxel in voxelObject)
            {
                for (var i = 0; i < processors.Count; i++)
                {
                    processors[i].Process(ref voxel);
                }
            }
        }

        if (_voxelProcessors.TryGetValue(VoxelProcessorPass.PostPass, out processors))
        {
            foreach (ref Voxel voxel in voxelObject)
            {
                for (var i = 0; i < processors.Count; i++)
                {
                    processors[i].Process(ref voxel);
                }
            }
        }
    }
    
    private void SampleVoxels(VoxelObject voxelObject)
    {
        if (_sampleProcessors.TryGetValue(VoxelProcessorPass.PrePass, out List<IVoxelSampleProcessor>? processors))
        {
            foreach (VoxelSample sample in voxelObject.GetSampler())
            {
                for (var i = 0; i < processors.Count; i++)
                {
                    processors[i].Process(sample);
                }
            }
        }

        if (_sampleProcessors.TryGetValue(VoxelProcessorPass.PostPass, out processors))
        {
            foreach (VoxelSample sample in voxelObject.GetSampler())
            {
                for (var i = 0; i < processors.Count; i++)
                {
                    processors[i].Process(sample);
                }
            }
        }
    }
}