namespace WaywardBeyond.Data.Buffs;

/// <summary>Parsed TOML row wrapper carrying either a <see cref="BuffDefinition"/> or a parse error.</summary>
internal readonly record struct BuffEntry(BuffDefinition Definition, string? Error);
