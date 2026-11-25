using System.Globalization;
using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;

namespace WaywardBeyond.AssetTools.Packs;

/// <summary>
///     Pack metadata stored in <c>pack.toml</c> at the pack root outside <c>assets/</c>.
/// </summary>
public readonly record struct PackMetadata(
    string Id,
    string Namespace,
    uint ContentVersion,
    string ToolVersion,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    ///     File name of the pack metadata descriptor at the pack root.
    /// </summary>
    public const string FILE_NAME = "pack.toml";

    /// <summary>
    ///     Loads <c>pack.toml</c> from <paramref name="packDirectory"/>.
    /// </summary>
    public static Result<PackMetadata> Load(string packDirectory)
    {
        if (string.IsNullOrWhiteSpace(packDirectory))
        {
            return Result<PackMetadata>.FromFailure("Pack directory is required.");
        }

        string filePath = Path.Combine(packDirectory, FILE_NAME);
        if (!File.Exists(filePath))
        {
            return Result<PackMetadata>.FromFailure($"Pack metadata file not found: '{filePath}'.");
        }

        Result<TomlDocument> loaded = TomlDocument.Load(filePath);
        if (!loaded.Success)
        {
            return Result<PackMetadata>.FromFailure(loaded.Message);
        }

        TomlDocument doc = loaded.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<PackMetadata>.FromFailure($"Pack metadata '{filePath}' contains TOML syntax errors.");
        }

        Result<TomlFieldSpan> idField = doc.GetRootField("ID");
        Result<TomlFieldSpan> nsField = doc.GetRootField("Namespace");
        Result<TomlFieldSpan> contentVerField = doc.GetRootField("ContentVersion");
        Result<TomlFieldSpan> toolVerField = doc.GetRootField("ToolVersion");
        Result<TomlFieldSpan> createdField = doc.GetRootField("CreatedAt");

        if (!idField.Success || idField.Value.TypedValue is not string id || string.IsNullOrWhiteSpace(id))
        {
            return Result<PackMetadata>.FromFailure("Pack metadata is missing required 'ID'.");
        }

        if (!nsField.Success || nsField.Value.TypedValue is not string packNamespace || string.IsNullOrWhiteSpace(packNamespace))
        {
            return Result<PackMetadata>.FromFailure("Pack metadata is missing required 'Namespace'.");
        }

        if (!contentVerField.Success || contentVerField.Value.TypedValue is not long rawVersion || rawVersion < 0 || rawVersion > uint.MaxValue)
        {
            return Result<PackMetadata>.FromFailure("Pack metadata is missing a valid unsigned integer 'ContentVersion'.");
        }

        string toolVersion = toolVerField.Success && toolVerField.Value.TypedValue is string tv ? tv : string.Empty;
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        if (createdField.Success &&
            createdField.Value.TypedValue is string createdStr &&
            DateTimeOffset.TryParse(createdStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsedCreated))
        {
            createdAt = parsedCreated;
        }

        return Result<PackMetadata>.FromSuccess(new PackMetadata(
            id,
            packNamespace,
            (uint)rawVersion,
            toolVersion,
            createdAt));
    }

    /// <summary>
    ///     Serializes this pack metadata to clean TOML text.
    /// </summary>
    public string ToToml()
    {
        var builder = new StringBuilder();
        builder.Append("ID             = ");
        builder.Append(TomlValueFormatter.FormatBasicString(Id));
        builder.Append('\n');
        builder.Append("Namespace      = ");
        builder.Append(TomlValueFormatter.FormatBasicString(Namespace));
        builder.Append('\n');
        builder.Append("ContentVersion = ");
        builder.Append(TomlValueFormatter.FormatInteger(ContentVersion));
        builder.Append('\n');
        builder.Append("ToolVersion    = ");
        builder.Append(TomlValueFormatter.FormatBasicString(ToolVersion));
        builder.Append('\n');
        builder.Append("CreatedAt      = ");
        builder.Append(TomlValueFormatter.FormatBasicString(CreatedAt.ToString("O", CultureInfo.InvariantCulture)));
        builder.Append('\n');
        return builder.ToString();
    }
}
