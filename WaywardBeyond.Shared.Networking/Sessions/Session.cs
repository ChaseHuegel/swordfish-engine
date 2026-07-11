using System;

namespace WaywardBeyond.Shared.Networking.Sessions;

public readonly struct Session(uint id) : IEquatable<Session>
{
    public readonly uint ID = id;

    public bool Equals(Session other) => ID == other.ID;

    public override bool Equals(object? obj) => obj is Session other && Equals(other);

    public override int GetHashCode() => (int)ID;

    public static bool operator ==(Session left, Session right) => left.Equals(right);

    public static bool operator !=(Session left, Session right) => !left.Equals(right);

    public override string ToString() => $"Session({ID})";
}
