using System.Security.Cryptography;

namespace Swordfish.ECS;

public readonly struct Uuid : IEquatable<Uuid>
{
    public static readonly Uuid Null = default;

    private readonly ulong _value;

    private Uuid(ulong value)
    {
        _value = value;
    }

    public static Uuid NewUuid()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return new Uuid(BitConverter.ToUInt64(bytes));
    }

    public static Uuid FromValue(ulong value)
    {
        return new Uuid(value);
    }

    public readonly ulong ToValue()
    {
        return _value;
    }

    public override string ToString()
    {
        return _value.ToString();
    }

    public bool Equals(Uuid other)
    {
        return _value == other._value;
    }

    public override bool Equals(object? obj)
    {
        return obj is Uuid other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    public static bool operator ==(Uuid a, Uuid b) => a.Equals(b);

    public static bool operator !=(Uuid a, Uuid b) => !a.Equals(b);
}
