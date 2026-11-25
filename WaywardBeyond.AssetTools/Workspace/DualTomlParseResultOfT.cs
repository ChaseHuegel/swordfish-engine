namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Side-by-side result of authoritative and strict Tomlet deserialization for one definition type.
/// </summary>
public readonly record struct DualTomlParseResult<T>(
    bool AuthoritativeSuccess,
    T? AuthoritativeValue,
    string? AuthoritativeError,
    bool StrictSuccess,
    T? StrictValue,
    string? StrictError);
