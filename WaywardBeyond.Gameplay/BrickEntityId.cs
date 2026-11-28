using Krill;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay;

/// <summary>Deterministic entity UUID derivation shared by client and server brick-entities.</summary>
public static class BrickEntityId
{
    private const ulong SALT = 0x517C_C1B7_2722_0A95UL;
    private const ulong SPLITMIX_INCREMENT = 0x9E37_79B9_7F4A_7C15UL;

    /// <summary>Derives the deterministic, non-zero entity UUID for (<paramref name="structureUuid"/>, <paramref name="coord"/>).</summary>
    public static Uuid Derive(Uuid structureUuid, Int3 coord)
    {
        ulong hash = MixHash(structureUuid.ToValue(), coord.X, coord.Y, coord.Z);
        Uuid candidate = Uuid.FromValue(hash);
        while (candidate == Uuid.Null)
        {
            hash = SplitMix64(hash + SPLITMIX_INCREMENT);
            candidate = Uuid.FromValue(hash);
        }

        return candidate;
    }

    /// <summary>Derives a deterministic, non-zero entity UUID for (<paramref name="structureUuid"/>, <paramref name="coord"/>) that is unused in <paramref name="store"/>.</summary>
    public static Uuid DeriveUnique(Uuid structureUuid, Int3 coord, DataStore store)
    {
        ulong hash = MixHash(structureUuid.ToValue(), coord.X, coord.Y, coord.Z);
        Uuid candidate = Uuid.FromValue(hash);
        while (candidate == Uuid.Null || store.TryGet(candidate, out _))
        {
            hash = SplitMix64(hash + SPLITMIX_INCREMENT);
            candidate = Uuid.FromValue(hash);
        }

        return candidate;
    }

    private static ulong MixHash(ulong structureUuid, int x, int y, int z)
    {
        ulong h = SplitMix64(structureUuid ^ SALT);
        h = SplitMix64(h ^ (uint)x);
        h = SplitMix64(h ^ ((ulong)(uint)y << 32));
        h = SplitMix64(h ^ (uint)z);
        return h;
    }

    private static ulong SplitMix64(ulong x)
    {
        x += SPLITMIX_INCREMENT;
        x = (x ^ (x >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D0_49BB_1331_11EBUL;
        return x ^ (x >> 31);
    }
}
