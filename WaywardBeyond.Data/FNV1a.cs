namespace WaywardBeyond.Data;

/// <summary>FNV1a non-cryptographic hashing algorithm.</summary>
public static class FNV1a
{
    /// <summary>Computes a 32 bit hash of the provided string.</summary>
    public static uint ComputeHash32(string str)
    {
        const uint fnvOffset = 0x811C9DC5;
        const uint fnvPrime = 0x01000193;

        uint hash = fnvOffset;
        for (var i = 0; i < str.Length; i++)
        {
            char c = str[i];
            hash ^= c;
            hash *= fnvPrime;
        }

        return hash;
    }
}