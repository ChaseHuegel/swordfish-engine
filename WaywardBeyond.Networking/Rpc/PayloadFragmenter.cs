using System;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>Splits one serialized payload into ordered parts and joins them back.</summary>
public static class PayloadFragmenter
{
    /// <summary>The largest payload one part carries, below the nsd-rpc default frame cap of 65,570 bytes.</summary>
    public const int FRAGMENT_BUDGET = 60_000;

    /// <summary>The most parts one payload may occupy.</summary>
    public const int MAX_PARTS = 512;

    /// <summary>The largest payload a fragmented group may assemble into.</summary>
    public const int MAX_PAYLOAD_BYTES = 16 * 1024 * 1024;

    /// <summary>The number of parts a payload of <paramref name="length"/> bytes occupies.</summary>
    public static int PartCount(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return length == 0 ? 1 : (length + FRAGMENT_BUDGET - 1) / FRAGMENT_BUDGET;
    }

    /// <summary>Copies one part of a payload.</summary>
    public static byte[] Slice(ReadOnlySpan<byte> payload, int partIndex)
    {
        int count = PartCount(payload.Length);
        if (partIndex < 0 || partIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(partIndex));
        }

        int start = partIndex * FRAGMENT_BUDGET;
        int length = Math.Min(FRAGMENT_BUDGET, payload.Length - start);
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
            length = checked(length + parts[index].Length);
        }

        if (length > MAX_PAYLOAD_BYTES)
        {
            throw new InvalidOperationException($"A fragmented payload may not exceed {MAX_PAYLOAD_BYTES} bytes.");
        }

        var payload = new byte[length];
        int offset = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            byte[] part = parts[index];
            part.CopyTo(payload, offset);
            offset += part.Length;
        }

        return payload;
    }
}
