using System;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Splits one serialized payload into fragments of at most <see cref="GameRpc.FRAGMENT_BUDGET"/> bytes
/// and joins them back. An empty payload yields one empty part, so the split always round-trips.
/// </summary>
public static class PayloadFragmenter
{
    /// <summary>The number of parts a payload of <paramref name="length"/> bytes occupies.</summary>
    public static int PartCount(int length, int budget = GameRpc.FRAGMENT_BUDGET)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (budget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        return length == 0 ? 1 : (length + budget - 1) / budget;
    }

    /// <summary>Splits a payload into parts. Each part is copied into its own array.</summary>
    public static byte[][] Split(ReadOnlySpan<byte> payload, int budget = GameRpc.FRAGMENT_BUDGET)
    {
        if (budget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        int count = PartCount(payload.Length, budget);
        var parts = new byte[count][];
        for (var index = 0; index < count; index++)
        {
            parts[index] = Slice(payload, index, budget);
        }

        return parts;
    }

    /// <summary>Copies one part of a payload.</summary>
    public static byte[] Slice(ReadOnlySpan<byte> payload, int partIndex, int budget = GameRpc.FRAGMENT_BUDGET)
    {
        if (budget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        int count = PartCount(payload.Length, budget);
        if (partIndex < 0 || partIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(partIndex));
        }

        int start = partIndex * budget;
        int length = Math.Min(budget, payload.Length - start);
        return payload.Slice(start, length).ToArray();
    }

    /// <summary>Joins parts in order. A part list must not be empty.</summary>
    public static byte[] Join(ReadOnlySpan<byte[]> parts)
    {
        if (parts.Length == 0)
        {
            throw new ArgumentException("At least one part is required.", nameof(parts));
        }

        int length = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            length = checked(length + (parts[index]?.Length ?? 0));
        }

        var payload = new byte[length];
        int offset = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            byte[] part = parts[index] ?? [];
            part.CopyTo(payload, offset);
            offset += part.Length;
        }

        return payload;
    }
}
