namespace WaywardBeyond.AssetTools.Documents;

/// <summary>
///     Syntax or semantic validation message produced while parsing a TOML document.
/// </summary>
public readonly record struct TomlDiagnostic(TomlTextSpan Span, string Message, bool IsError);
