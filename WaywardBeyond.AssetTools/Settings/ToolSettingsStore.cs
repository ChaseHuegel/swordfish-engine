using System.Text;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Storage;

namespace WaywardBeyond.AssetTools.Settings;

/// <summary>
///     Loads and atomically saves <see cref="ToolSettings"/> as TOML.
/// </summary>
public sealed class ToolSettingsStore
{
    private readonly ToolDataPaths _paths;
    private readonly AtomicFileSaver _atomicSaver;

    /// <summary>
    ///     Creates a settings store backed by <paramref name="paths"/>.
    /// </summary>
    public ToolSettingsStore(ToolDataPaths paths, AtomicFileSaver? atomicSaver = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _atomicSaver = atomicSaver ?? new AtomicFileSaver();
    }

    /// <summary>
    ///     Loads settings from disk, returning defaults when no settings file exists yet.
    /// </summary>
    public Result<ToolSettings> Load()
    {
        if (!File.Exists(_paths.SettingsFilePath))
        {
            return Result<ToolSettings>.FromSuccess(new ToolSettings());
        }

        Result<TomlDocument> docResult = TomlDocument.Load(_paths.SettingsFilePath);
        if (!docResult.Success)
        {
            return Result<ToolSettings>.FromFailure(docResult.Message);
        }

        TomlDocument document = docResult.Value;
        if (document.HasSyntaxErrors)
        {
            return Result<ToolSettings>.FromFailure($"Settings file '{_paths.SettingsFilePath}' contains invalid TOML syntax.");
        }

        var settings = new ToolSettings();

        Result<TomlFieldSpan> cultureField = document.GetRootField("CultureOverride");
        if (cultureField.Success && cultureField.Value.TypedValue is string culture)
        {
            settings.CultureOverride = culture;
        }

        Result<TomlFieldSpan> themeField = document.GetRootField("Theme");
        if (themeField.Success && themeField.Value.TypedValue is string themeName &&
            Enum.TryParse(themeName, ignoreCase: true, out ToolTheme parsedTheme))
        {
            settings.Theme = parsedTheme;
        }

        Result<TomlFieldSpan> uiScaleField = document.GetRootField("UiScale");
        if (uiScaleField.Success)
        {
            double parsedScale = uiScaleField.Value.TypedValue switch
            {
                double d => d,
                long l => l,
                _ => settings.UiScale
            };
            settings.UiScale = ToolSettings.ClampUiScale(parsedScale);
        }

        Result<TomlFieldSpan> glField = document.GetRootField("GlPreference");
        if (glField.Success && glField.Value.TypedValue is string glName &&
            Enum.TryParse(glName, ignoreCase: true, out GlBackendPreference parsedGl))
        {
            settings.GlPreference = parsedGl;
        }

        Result<TomlFieldSpan> logField = document.GetRootField("LogLevel");
        if (logField.Success && logField.Value.TypedValue is string logName &&
            Enum.TryParse(logName, ignoreCase: true, out ToolLogLevel parsedLog))
        {
            settings.LogLevel = parsedLog;
        }

        Result<TomlFieldSpan> recentsField = document.GetRootField("RecentWorkspaces");
        if (recentsField.Success && recentsField.Value.ValueKind == TomlValueKind.Array)
        {
            for (int i = 0; i < recentsField.Value.ArrayElements.Count; i++)
            {
                if (recentsField.Value.ArrayElements[i].TypedValue is string workspace &&
                    !string.IsNullOrWhiteSpace(workspace))
                {
                    settings.RecentWorkspaces.Add(workspace);
                }
            }
        }

        LoadPanelLayout(document, settings.PanelLayout);
        LoadWorkspaceNamespaces(document, settings);
        return Result<ToolSettings>.FromSuccess(settings);
    }

    /// <summary>
    ///     Serializes <paramref name="settings"/> to TOML and writes it atomically to disk.
    /// </summary>
    public Result Save(ToolSettings settings)
    {
        if (settings == null)
        {
            return Result.FromFailure("Settings instance is required.");
        }

        string toml = Serialize(settings);
        var request = new FileSaveRequest(_paths.SettingsFilePath, Encoding.UTF8.GetBytes(toml));
        return _atomicSaver.Save(in request).ToResult();
    }

    private static void LoadPanelLayout(TomlDocument document, PanelLayoutSettings layout)
    {
        Result<TomlTableSpan> layoutTable = document.GetTable("PanelLayout");
        if (layoutTable.Success)
        {
            layout.LeftWidth = ReadDouble(document, layoutTable.Value.Index, "LeftWidth", layout.LeftWidth);
            layout.RightWidth = ReadDouble(document, layoutTable.Value.Index, "RightWidth", layout.RightWidth);
            layout.BottomHeight = ReadDouble(document, layoutTable.Value.Index, "BottomHeight", layout.BottomHeight);
            layout.IsValidationOpen = ReadBool(document, layoutTable.Value.Index, "IsValidationOpen", layout.IsValidationOpen);
            layout.IsPreviewOpen = ReadBool(document, layoutTable.Value.Index, "IsPreviewOpen", layout.IsPreviewOpen);
        }

        Result<TomlTableSpan> collapsedTable = document.GetTable("PanelLayout.CollapsedSections");
        if (collapsedTable.Success)
        {
            IReadOnlyList<TomlFieldSpan> fields = collapsedTable.Value.Fields;
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].TypedValue is bool collapsed)
                {
                    layout.CollapsedSections[fields[i].Key] = collapsed;
                }
            }
        }
    }

    private static void LoadWorkspaceNamespaces(TomlDocument document, ToolSettings settings)
    {
        Result<TomlTableSpan> namespacesTable = document.GetTable("WorkspaceNamespaces");
        if (!namespacesTable.Success)
        {
            return;
        }

        IReadOnlyList<TomlFieldSpan> fields = namespacesTable.Value.Fields;
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].TypedValue is string defaultNamespace && !string.IsNullOrWhiteSpace(defaultNamespace))
            {
                settings.SetDefaultNamespace(fields[i].Key, defaultNamespace);
            }
        }
    }

    private static double ReadDouble(TomlDocument document, int tableIndex, string key, double fallback)
    {
        Result<TomlFieldSpan> field = document.GetFieldInTable(tableIndex, key);
        if (!field.Success)
        {
            return fallback;
        }

        return field.Value.TypedValue switch
        {
            double d => d,
            long l => l,
            _ => fallback
        };
    }

    private static bool ReadBool(TomlDocument document, int tableIndex, string key, bool fallback)
    {
        Result<TomlFieldSpan> field = document.GetFieldInTable(tableIndex, key);
        return field.Success && field.Value.TypedValue is bool value ? value : fallback;
    }

    private static string Serialize(ToolSettings settings)
    {
        var builder = new StringBuilder();
        builder.Append("CultureOverride = ");
        builder.Append(TomlValueFormatter.FormatBasicString(settings.CultureOverride ?? string.Empty));
        builder.Append('\n');
        builder.Append("Theme = ");
        builder.Append(TomlValueFormatter.FormatBasicString(settings.Theme.ToString()));
        builder.Append('\n');
        builder.Append("UiScale = ");
        builder.Append(TomlValueFormatter.FormatFloat(ToolSettings.ClampUiScale(settings.UiScale)));
        builder.Append('\n');
        builder.Append("GlPreference = ");
        builder.Append(TomlValueFormatter.FormatBasicString(settings.GlPreference.ToString()));
        builder.Append('\n');
        builder.Append("LogLevel = ");
        builder.Append(TomlValueFormatter.FormatBasicString(settings.LogLevel.ToString()));
        builder.Append('\n');
        builder.Append("RecentWorkspaces = ");
        builder.Append(TomlValueFormatter.FormatStringArray(
            settings.RecentWorkspaces,
            multiline: settings.RecentWorkspaces.Count > 0,
            lineEnding: "\n",
            itemIndent: "    ",
            closeIndent: string.Empty,
            trailingComma: true));
        builder.Append('\n');

        builder.Append('\n');
        builder.Append("[PanelLayout]\n");
        builder.Append("LeftWidth = ");
        builder.Append(TomlValueFormatter.FormatFloat(settings.PanelLayout.LeftWidth));
        builder.Append('\n');
        builder.Append("RightWidth = ");
        builder.Append(TomlValueFormatter.FormatFloat(settings.PanelLayout.RightWidth));
        builder.Append('\n');
        builder.Append("BottomHeight = ");
        builder.Append(TomlValueFormatter.FormatFloat(settings.PanelLayout.BottomHeight));
        builder.Append('\n');
        builder.Append("IsValidationOpen = ");
        builder.Append(TomlValueFormatter.FormatBoolean(settings.PanelLayout.IsValidationOpen));
        builder.Append('\n');
        builder.Append("IsPreviewOpen = ");
        builder.Append(TomlValueFormatter.FormatBoolean(settings.PanelLayout.IsPreviewOpen));
        builder.Append('\n');

        if (settings.PanelLayout.CollapsedSections.Count > 0)
        {
            builder.Append('\n');
            builder.Append("[PanelLayout.CollapsedSections]\n");
            List<string> keys = [.. settings.PanelLayout.CollapsedSections.Keys];
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                builder.Append(TomlValueFormatter.FormatKeySegment(key));
                builder.Append(" = ");
                builder.Append(TomlValueFormatter.FormatBoolean(settings.PanelLayout.CollapsedSections[key]));
                builder.Append('\n');
            }
        }

        if (settings.WorkspaceDefaultNamespaces.Count > 0)
        {
            List<string> workspaceKeys = [.. settings.WorkspaceDefaultNamespaces.Keys];
            workspaceKeys.Sort(StringComparer.Ordinal);

            bool wroteHeader = false;
            for (int i = 0; i < workspaceKeys.Count; i++)
            {
                string key = workspaceKeys[i];
                string value = settings.WorkspaceDefaultNamespaces[key];
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!wroteHeader)
                {
                    builder.Append('\n');
                    builder.Append("[WorkspaceNamespaces]\n");
                    wroteHeader = true;
                }

                builder.Append(TomlValueFormatter.FormatKeySegment(key));
                builder.Append(" = ");
                builder.Append(TomlValueFormatter.FormatBasicString(value.Trim()));
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }
}
