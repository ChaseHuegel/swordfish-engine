namespace Swordfish.Graphics.Particles;

/// <summary>Represents an inclusive scalar range sampled uniformly per particle.</summary>
public readonly record struct ParticleRange(float Min, float Max)
{
    /// <summary>Creates a degenerate range that always returns <paramref name="value"/>.</summary>
    public static ParticleRange Constant(float value) => new(value, value);

    /// <summary>Samples a value uniformly within [<see cref="Min"/>, <see cref="Max"/>].</summary>
    public float Sample(Random random)
    {
        if (Max <= Min)
        {
            return Min;
        }

        return Min + (float)random.NextDouble() * (Max - Min);
    }
}
