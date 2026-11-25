using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Storage;
using GameContentVersion = WaywardBeyond.Data.ContentVersion;

namespace WaywardBeyond.AssetTools.Packs;

/// <summary>
///     Scaffolds a new content pack directory containing <c>manifest.toml</c>, <c>pack.toml</c>, and standard <c>assets/</c> folders.
/// </summary>
public sealed class PackScaffolder
{
    private static readonly string[] ASSET_SUBDIRECTORIES =
    [
        "bricks",
        "items",
        "paints",
        "skills",
        "bodies",
        "materials",
        "textures/block",
        "textures/items",
        "meshes",
        "audio",
        "lang/en",
        "lang/tags",
        "blueprints"
    ];

    private readonly AtomicFileSaver _atomicSaver;

    /// <summary>
    ///     Creates a pack scaffolder using <paramref name="atomicSaver"/> for file creation.
    /// </summary>
    public PackScaffolder(AtomicFileSaver? atomicSaver = null)
    {
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
    }

    /// <summary>
    ///     Scaffolds a new content pack inside <paramref name="directory"/>.
    /// </summary>
    public Result<PackMetadata> CreatePack(
        string directory,
        string packId,
        string packNamespace,
        string displayName,
        string author,
        string toolVersion)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Result<PackMetadata>.FromFailure("Target pack directory is required.");
        }

        if (string.IsNullOrWhiteSpace(packId) || string.IsNullOrWhiteSpace(packNamespace))
        {
            return Result<PackMetadata>.FromFailure("Pack ID and namespace are required.");
        }

        string fullDir = Path.GetFullPath(directory);
        string manifestPath = Path.Combine(fullDir, "manifest.toml");
        string packTomlPath = Path.Combine(fullDir, PackMetadata.FILE_NAME);
        if (File.Exists(manifestPath) || File.Exists(packTomlPath))
        {
            return Result<PackMetadata>.FromFailure($"Directory '{fullDir}' already contains pack or module metadata.");
        }

        try
        {
            Directory.CreateDirectory(fullDir);
            string assetsRoot = Path.Combine(fullDir, "assets");
            for (int i = 0; i < ASSET_SUBDIRECTORIES.Length; i++)
            {
                Directory.CreateDirectory(Path.Combine(assetsRoot, ASSET_SUBDIRECTORIES[i]));
            }

            string resolvedName = string.IsNullOrWhiteSpace(displayName) ? packId : displayName.Trim();
            string resolvedAuthor = string.IsNullOrWhiteSpace(author) ? "Unknown" : author.Trim();
            string manifestContent = BuildManifestToml(packId.Trim(), resolvedName, resolvedAuthor);
            var manifestReq = new FileSaveRequest(manifestPath, Encoding.UTF8.GetBytes(manifestContent), ExpectFileNotToExist: true);
            AtomicSaveResult manifestSave = _atomicSaver.Save(in manifestReq);
            if (!manifestSave.Success)
            {
                return Result<PackMetadata>.FromFailure(manifestSave.ErrorMessage);
            }

            var metadata = new PackMetadata(
                packId.Trim(),
                packNamespace.Trim(),
                GameContentVersion.CURRENT,
                string.IsNullOrWhiteSpace(toolVersion) ? "0.1.0" : toolVersion.Trim(),
                DateTimeOffset.UtcNow);

            var packReq = new FileSaveRequest(packTomlPath, Encoding.UTF8.GetBytes(metadata.ToToml()), ExpectFileNotToExist: true);
            AtomicSaveResult packSave = _atomicSaver.Save(in packReq);
            if (!packSave.Success)
            {
                return Result<PackMetadata>.FromFailure(packSave.ErrorMessage);
            }

            return Result<PackMetadata>.FromSuccess(metadata);
        }
        catch (Exception ex)
        {
            return Result<PackMetadata>.FromFailure(ex);
        }
    }

    private static string BuildManifestToml(string id, string name, string author)
    {
        var builder = new StringBuilder();
        builder.Append("ID          = ");
        builder.Append(TomlValueFormatter.FormatBasicString(id));
        builder.Append('\n');
        builder.Append("Name        = ");
        builder.Append(TomlValueFormatter.FormatBasicString(name));
        builder.Append('\n');
        builder.Append("Description = \"Wayward Beyond content pack.\"\n");
        builder.Append("Author      = ");
        builder.Append(TomlValueFormatter.FormatBasicString(author));
        builder.Append('\n');
        builder.Append("Assemblies  = []\n");
        return builder.ToString();
    }
}
