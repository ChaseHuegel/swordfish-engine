namespace Swordfish.Graphics.Particles;

/// <summary>Triple-buffered CPU-to-render snapshot holding packed particle instances and batches.</summary>
internal sealed class ParticleRenderSnapshot
{
    public ParticleInstance[] Instances = [];
    public int InstanceCount;
    public ParticleRenderBatch[] Batches = [];
    public int BatchCount;

    /// <summary>Clears counts and batch references without shrinking backing arrays.</summary>
    public void Reset()
    {
        if (BatchCount > 0)
        {
            Array.Clear(Batches, 0, BatchCount);
        }

        InstanceCount = 0;
        BatchCount = 0;
    }

    /// <summary>Ensures <see cref="Instances"/> can store at least <paramref name="required"/> elements.</summary>
    public void EnsureInstanceCapacity(int required)
    {
        if (Instances.Length >= required)
        {
            return;
        }

        int next = Math.Max(required, Math.Max(64, Instances.Length * 2));
        Array.Resize(ref Instances, next);
    }

    /// <summary>Ensures <see cref="Batches"/> can store at least <paramref name="required"/> elements.</summary>
    public void EnsureBatchCapacity(int required)
    {
        if (Batches.Length >= required)
        {
            return;
        }

        int next = Math.Max(required, Math.Max(8, Batches.Length * 2));
        Array.Resize(ref Batches, next);
    }
}
