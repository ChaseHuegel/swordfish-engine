namespace WaywardBeyond.Client.Core.Serialization;

internal static class FNV1a
{
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