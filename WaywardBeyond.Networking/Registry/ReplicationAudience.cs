namespace WaywardBeyond.Networking.Registry;

/// <summary>Declares which connected clients receive a server-owned component.</summary>
public enum ReplicationAudience : byte
{
    /// <summary>Replicates to every connected client.</summary>
    All = 0,

    /// <summary>Replicates only to the client whose session entity owns the entity.</summary>
    OwnerOnly = 1,

    /// <summary>Replicates to every connected client except the entity's owning client.</summary>
    OthersOnly = 2,
}
