using System;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Types;

public struct ByteSize(in long bytes)
{
    public const long KB = 1024;
    public const long MB = KB * 1024;
    public const long GB = MB * 1024;

    private const long HALF_KB = KB / 2;
    private const long HALF_MB = MB / 2;
    private const long HALF_GB = GB / 2;

    public readonly long Bytes = bytes;
    public double Kilobytes => Bytes / (double)KB;
    public double Megabytes => Bytes / (double)MB;
    public double Gigabytes => Bytes / (double)GB;

    public override string ToString()
    {
        return Bytes switch
        {
            < HALF_KB => $"{Bytes:0} bytes",
            < HALF_MB => $"{Kilobytes:0.00} KB",
            < HALF_GB => $"{Megabytes:0.00} MB",
            _ => $"{Gigabytes:0.00} GB",
        };
    }

    public override bool Equals(object obj)
    {
        return obj is ByteSize other && other.Bytes == Bytes;
    }

    public override int GetHashCode()
    {
        return Bytes.GetHashCode();
    }

    public static bool operator ==(ByteSize b1, ByteSize b2)
    {
        return b1.Bytes == b2.Bytes;
    }

    public static bool operator >(ByteSize b1, ByteSize b2)
    {
        return b1.Bytes > b2.Bytes;
    }

    public static bool operator >=(ByteSize b1, ByteSize b2)
    {
        return b1.Bytes >= b2.Bytes;
    }

    public static bool operator !=(ByteSize b1, ByteSize b2)
    {
        return b1.Bytes != b2.Bytes;
    }

    public static bool operator <(ByteSize b1, ByteSize b2)
    {
        return b1.Bytes < b2.Bytes;
    }

    public static bool operator <=(ByteSize b1, ByteSize b2)
    {
        return b1.Bytes <= b2.Bytes;
    }

    public static ByteSize operator +(ByteSize b1, ByteSize b2)
    {
        return new ByteSize(b1.Bytes + b2.Bytes);
    }

    public static ByteSize operator -(ByteSize b1, ByteSize b2)
    {
        return new ByteSize(b1.Bytes - b2.Bytes);
    }

    public static ByteSize operator *(ByteSize b1, ByteSize b2)
    {
        return new ByteSize(b1.Bytes * b2.Bytes);
    }

    public static ByteSize operator /(ByteSize b1, ByteSize b2)
    {
        return new ByteSize(b1.Bytes / b2.Bytes);
    }

    public static ByteSize operator +(ByteSize b)
    {
        return new ByteSize(Math.Abs(b.Bytes));
    }

    public static ByteSize operator -(ByteSize b)
    {
        return new ByteSize(-b.Bytes);
    }

    public static ByteSize FromBytes(long bytes) => new(bytes);

    public static ByteSize FromKilobytes(long kilobytes) => new(kilobytes * KB);

    public static ByteSize FromMegabytes(long megabytes) => new(megabytes * MB);

    public static ByteSize FromGigabytes(long gigabytes) => new(gigabytes * GB);
}