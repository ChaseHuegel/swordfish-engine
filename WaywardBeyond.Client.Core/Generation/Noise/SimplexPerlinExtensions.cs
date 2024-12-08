using LibNoise.Primitive;

namespace WaywardBeyond.Client.Core.Generation.Noise;

internal static class SimplexPerlinExtensions
{
    public static float GetLayeredNoiseValue(this SimplexPerlin simplexPerlin, int octaves, float frequency, float amplitude, int x, int y, int z)
    {
        var value = 0f;
        for (var octave = 0; octave < octaves; octave++)
        {
            value += simplexPerlin.GetValue(x * frequency, y * frequency, z * frequency) * amplitude;
            frequency *= 2;
            amplitude /= 2;
        }

        value /= octaves;
        float normalizedValue = (value + 1f) / 2f;
        return normalizedValue;
    }
}