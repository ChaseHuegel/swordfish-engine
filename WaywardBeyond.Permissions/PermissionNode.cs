namespace WaywardBeyond.Permissions;

/// <summary>A parsed permission node: a deny flag, a global or trailing wildcard, and the key or prefix.</summary>
internal readonly struct PermissionNode(bool isDeny, bool isGlobal, bool isWildcard, string key)
{
    /// <summary>True when the node is prefixed with the deny marker.</summary>
    public readonly bool IsDeny = isDeny;

    /// <summary>True when the node is the global wildcard.</summary>
    public readonly bool IsGlobal = isGlobal;

    /// <summary>True when the node ends with a wildcard suffix.</summary>
    public readonly bool IsWildcard = isWildcard;

    /// <summary>The permission key, or the wildcard prefix.</summary>
    public readonly string Key = key;
}
