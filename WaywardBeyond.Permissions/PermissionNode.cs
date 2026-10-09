namespace WaywardBeyond.Permissions;

/// <summary>A parsed permission node: a deny flag, a global or trailing wildcard, and the key or prefix.</summary>
internal readonly struct PermissionNode(bool isDeny, bool isGlobal, bool isWildcard, string key)
{
    public readonly bool IsDeny = isDeny;
    public readonly bool IsGlobal = isGlobal;
    public readonly bool IsWildcard = isWildcard;
    public readonly string Key = key;
}
