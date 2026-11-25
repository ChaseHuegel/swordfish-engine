using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Uses Avalonia's native <see cref="IStorageProvider"/> to pick workspace folders and files on desktop.
/// </summary>
internal sealed class DesktopWorkspacePathPicker : IWorkspacePathPicker
{
    private readonly Func<TopLevel?> _resolveTopLevel;

    /// <summary>
    ///     Initializes a native picker bound to the active Avalonia top-level window.
    /// </summary>
    public DesktopWorkspacePathPicker(Func<TopLevel?> resolveTopLevel)
    {
        _resolveTopLevel = resolveTopLevel;
    }

    /// <inheritdoc/>
    public async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = _resolveTopLevel();
        if (topLevel == null || !topLevel.StorageProvider.CanPickFolder)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        if (folders.Count == 0)
        {
            return null;
        }

        return folders[0].TryGetLocalPath();
    }

    /// <inheritdoc/>
    public Task<string?> PickBlueprintFileAsync(string title) =>
        PickFileAsync(title, null, "Wayward Beyond Blueprint", ["*.wbp"], "blueprint picker");

    /// <inheritdoc/>
    public Task<string?> PickImageFileAsync(string title, string? suggestedStartDirectory = null) =>
        PickFileAsync(
            title,
            suggestedStartDirectory,
            "Image Files",
            ["*.png", "*.bmp", "*.jpg", "*.jpeg", "*.webp"],
            "image picker");

    /// <inheritdoc/>
    public Task<string?> PickMeshFileAsync(string title, string? suggestedStartDirectory = null) =>
        PickFileAsync(
            title,
            suggestedStartDirectory,
            "Wavefront OBJ Mesh",
            ["*.obj"],
            "mesh picker");

    private async Task<string?> PickFileAsync(
        string title,
        string? suggestedStartDirectory,
        string filterName,
        IReadOnlyList<string> patterns,
        string pickerContext)
    {
        var topLevel = _resolveTopLevel();
        if (topLevel == null || !topLevel.StorageProvider.CanOpen)
        {
            return null;
        }

        IStorageFolder? startFolder = null;
        if (!string.IsNullOrWhiteSpace(suggestedStartDirectory) && Directory.Exists(suggestedStartDirectory))
        {
            try
            {
                startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(suggestedStartDirectory);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed resolving suggested {pickerContext} directory '{suggestedStartDirectory}': {ex.Message}");
            }
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
            FileTypeFilter =
            [
                new FilePickerFileType(filterName)
                {
                    Patterns = patterns,
                },
            ],
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }
}
