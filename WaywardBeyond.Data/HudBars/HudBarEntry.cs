namespace WaywardBeyond.Data.HudBars;

/// <summary>Parsed TOML row wrapper carrying either a <see cref="HudBarDefinition"/> or a parse error.</summary>
internal readonly record struct HudBarEntry(HudBarDefinition Definition, string? Error);
