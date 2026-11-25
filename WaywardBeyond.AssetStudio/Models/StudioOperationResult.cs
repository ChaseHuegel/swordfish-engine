namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Represents the non-throwing outcome of a workspace command.
/// </summary>
public readonly record struct StudioOperationResult(bool Success, string Message, string? Detail = null);
