namespace WaywardBeyond.Shared.Networking.Registry;

/// <summary>Which endpoint is authoritative over a networked component type's data.</summary>
public enum NetworkDirection
{
    /// <summary>Server owns the data; replicated downstream to clients.</summary>
    ServerOwned = 0,

    /// <summary>Client owns the data; replicated upstream to the server.</summary>
    ClientOwned = 1,
}