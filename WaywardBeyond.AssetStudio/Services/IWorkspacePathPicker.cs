namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Provides native OS folder and file selection dialogs for desktop workspace entry workflows.
/// </summary>
public interface IWorkspacePathPicker
{
    /// <summary>
    ///     Prompts the user to choose a directory on disk, or returns null when cancelled.
    /// </summary>
    Task<string?> PickFolderAsync(string title);

    /// <summary>
    ///     Prompts the user to choose a blueprint file on disk, or returns null when cancelled.
    /// </summary>
    Task<string?> PickBlueprintFileAsync(string title);

    /// <summary>
    ///     Prompts the user to choose an image file on disk, or returns null when cancelled.
    /// </summary>
    Task<string?> PickImageFileAsync(string title, string? suggestedStartDirectory = null);

    /// <summary>
    ///     Prompts the user to choose a Wavefront OBJ mesh file on disk, or returns null when cancelled.
    /// </summary>
    Task<string?> PickMeshFileAsync(string title, string? suggestedStartDirectory = null) => Task.FromResult<string?>(null);
}
