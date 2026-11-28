using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Scaffolds new asset entries in new or existing TOML files and creates localization keys.
/// </summary>
public sealed class AssetScaffolder
{
    /// <summary>
    ///     Appends the <c>.toml</c> extension to <paramref name="filePath"/> for TOML-backed asset kinds when omitted.
    /// </summary>
    public static string NormalizeTargetFilePath(AssetKind kind, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        string trimmed = filePath.Trim();
        if (kind == AssetKind.Blueprint)
        {
            return trimmed;
        }

        return trimmed.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : string.Concat(trimmed, ".toml");
    }

    /// <summary>
    ///     Creates a new TOML document containing a scaffolded definition for <paramref name="kind"/>.
    /// </summary>
    public Result<TomlDocument> CreateInNewFile(AssetKind kind, string assetId, string? sourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Result<TomlDocument>.FromFailure("Asset identifier is required.");
        }

        string? template = BuildNewFileTemplate(kind, assetId.Trim());
        if (template == null)
        {
            return Result<TomlDocument>.FromFailure($"Scaffolding is not supported for asset kind '{kind}'.");
        }

        string? normalizedSourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? sourcePath
            : NormalizeTargetFilePath(kind, sourcePath);

        TomlDocument document = TomlDocument.Parse(template, normalizedSourcePath);
        return document.HasSyntaxErrors
            ? Result<TomlDocument>.FromFailure("Generated asset template failed TOML validation.")
            : Result<TomlDocument>.FromSuccess(document);
    }

    /// <summary>
    ///     Appends a new <c>[[Kind]]</c> asset entry to an existing multi-entry TOML document.
    /// </summary>
    public Result<TomlTableSpan> AppendToExistingFile(AssetKind kind, string assetId, TomlDocument existingDocument)
    {
        if (existingDocument == null)
        {
            return Result<TomlTableSpan>.FromFailure("Existing document is required.");
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Result<TomlTableSpan>.FromFailure("Asset identifier is required.");
        }

        string id = assetId.Trim();
        (string? tablePath, List<KeyValuePair<string, string>>? fields) = BuildArrayEntryFields(kind, id);
        if (tablePath == null || fields == null)
        {
            return Result<TomlTableSpan>.FromFailure($"Appending entries is not supported for asset kind '{kind}'.");
        }

        return existingDocument.AppendArrayTable(tablePath, fields);
    }

    /// <summary>
    ///     Adds a localization key and text value to a new or existing <c>lang/&lt;locale&gt;/*.toml</c> file.
    /// </summary>
    public Result<TomlDocument> CreateLocalizationKey(
        string targetLangFilePath,
        string locale,
        string key,
        string initialText)
    {
        if (string.IsNullOrWhiteSpace(targetLangFilePath) ||
            string.IsNullOrWhiteSpace(locale) ||
            string.IsNullOrWhiteSpace(key))
        {
            return Result<TomlDocument>.FromFailure("Target file path, locale, and key are required.");
        }

        string normalizedPath = NormalizeTargetFilePath(AssetKind.Localization, targetLangFilePath);
        string formattedValue = TomlValueFormatter.FormatBasicString(initialText ?? string.Empty);

        if (!File.Exists(normalizedPath))
        {
            string formattedLocale = TomlValueFormatter.FormatBasicString(locale.Trim());
            string formattedKey = TomlValueFormatter.FormatBasicString(key.Trim());
            string content =
                $"\"Language\" = {formattedLocale}\n" +
                "\n" +
                "[Translations]\n" +
                $"{formattedKey} = {formattedValue}\n";
            return Result<TomlDocument>.FromSuccess(TomlDocument.Parse(content, normalizedPath));
        }

        Result<TomlDocument> loaded = TomlDocument.Load(normalizedPath);
        if (!loaded.Success)
        {
            return loaded;
        }

        TomlDocument document = loaded.Value;
        if (document.HasSyntaxErrors)
        {
            return Result<TomlDocument>.FromFailure($"Localization file '{normalizedPath}' has TOML syntax errors.");
        }

        Result<TomlTableSpan> translationsTable = document.GetTable("Translations");
        if (!translationsTable.Success)
        {
            Result<TomlTableSpan> added = document.AddTable(
                "Translations",
                [new KeyValuePair<string, string>(key.Trim(), formattedValue)]);
            return added.Success
                ? Result<TomlDocument>.FromSuccess(document)
                : Result<TomlDocument>.FromFailure(added.Message);
        }

        Result addField = document.AddField(translationsTable.Value, key.Trim(), formattedValue);
        return addField.Success
            ? Result<TomlDocument>.FromSuccess(document)
            : Result<TomlDocument>.FromFailure(addField.Message);
    }

    private static string? BuildNewFileTemplate(AssetKind kind, string id)
    {
        string qId = TomlValueFormatter.FormatBasicString(id);
        string slug = ExtractSlug(id);
        return kind switch
        {
            AssetKind.Brick =>
                "[[Bricks]]\n" +
                $"ID    = {qId}\n" +
                "Shape = \"block\"\n" +
                "[Bricks.Sounds]\n" +
                "Place = [\"metal_place\"]\n" +
                "Break = [\"metal_break\"]\n" +
                "[Bricks.Textures]\n" +
                $"Default = [{TomlValueFormatter.FormatBasicString(slug)}]\n",

            AssetKind.Item =>
                "[[Items]]\n" +
                $"ID          = {qId}\n" +
                $"Name        = {TomlValueFormatter.FormatBasicString($"item.{slug}.name")}\n" +
                $"Description = {TomlValueFormatter.FormatBasicString($"item.{slug}.description")}\n" +
                "MaxStack    = 100\n",

            AssetKind.Paint =>
                "[[Paints]]\n" +
                $"ID    = {qId}\n" +
                $"Name  = {TomlValueFormatter.FormatBasicString($"paint.{slug}.name")}\n" +
                "Color = \"#ffffff\"\n",

            AssetKind.Skill =>
                "[[Skills]]\n" +
                $"ID   = {qId}\n" +
                $"Name = {TomlValueFormatter.FormatBasicString($"skill.name.{slug}")}\n" +
                "\n" +
                "[Skills.Levels]\n" +
                "1 = 100\n",

            AssetKind.Body =>
                "[[Bodies]]\n" +
                $"ID      = {qId}\n" +
                $"Species = {TomlValueFormatter.FormatBasicString(slug)}\n" +
                "Gender  = \"Neutral\"\n" +
                "\n" +
                "[Bodies.States.standing]\n" +
                $"front = [{TomlValueFormatter.FormatBasicString($"characters/{slug}_standing.png")}]\n",

            AssetKind.Material =>
                "Shader   = \"textured\"\n" +
                "Textures = [\n" +
                $"    {TomlValueFormatter.FormatBasicString($"block/{slug}.png")},\n" +
                "]\n",

            AssetKind.Recipe =>
                "[[Recipes]]\n" +
                $"ID              = {qId}\n" +
                "TargetTag       = \"station.workbench\"\n" +
                "Mode            = \"Manual\"\n" +
                "DurationSeconds = 2.0\n" +
                "PowerKw         = 0.0\n" +
                "Priority        = 0\n" +
                "\n" +
                "[[Recipes.Inputs]]\n" +
                "ItemId = \"\"\n" +
                "Count  = 1\n" +
                "\n" +
                "[[Recipes.Outputs]]\n" +
                "ItemId = \"\"\n" +
                "Count  = 1\n",

            AssetKind.SlotGroup =>
                "[[SlotGroups]]\n" +
                $"ID       = {qId}\n" +
                "Capacity = 1\n",

            _ => null
        };
    }

    private static (string? TablePath, List<KeyValuePair<string, string>>? Fields) BuildArrayEntryFields(AssetKind kind, string id)
    {
        string qId = TomlValueFormatter.FormatBasicString(id);
        string slug = ExtractSlug(id);
        return kind switch
        {
            AssetKind.Brick => ("Bricks",
            [
                new("ID", qId),
                new("Shape", "\"block\"")
            ]),
            AssetKind.Item => ("Items",
            [
                new("ID", qId),
                new("Name", TomlValueFormatter.FormatBasicString($"item.{slug}.name")),
                new("MaxStack", "100")
            ]),
            AssetKind.Paint => ("Paints",
            [
                new("ID", qId),
                new("Name", TomlValueFormatter.FormatBasicString($"paint.{slug}.name")),
                new("Color", "\"#ffffff\"")
            ]),
            AssetKind.Skill => ("Skills",
            [
                new("ID", qId),
                new("Name", TomlValueFormatter.FormatBasicString($"skill.name.{slug}"))
            ]),
            AssetKind.Body => ("Bodies",
            [
                new("ID", qId),
                new("Species", TomlValueFormatter.FormatBasicString(slug)),
                new("Gender", "\"Neutral\"")
            ]),
            AssetKind.Recipe => ("Recipes",
            [
                new("ID", qId),
                new("TargetTag", "\"station.workbench\""),
                new("Mode", "\"Manual\""),
                new("DurationSeconds", "2.0"),
                new("PowerKw", "0.0"),
                new("Priority", "0")
            ]),
            AssetKind.SlotGroup => ("SlotGroups",
            [
                new("ID", qId),
                new("Capacity", "1")
            ]),
            _ => (null, null)
        };
    }

    private static string ExtractSlug(string assetId)
    {
        int colon = assetId.IndexOf(':');
        string raw = colon >= 0 && colon + 1 < assetId.Length ? assetId.Substring(colon + 1) : assetId;
        return raw.Replace('/', '_').Replace(' ', '_');
    }
}
