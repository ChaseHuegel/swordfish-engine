using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A category of settings rows shown in the tray or tab strip.</summary>
internal sealed class SettingsCategory(
    string id,
    string titleKey,
    SettingsRow[] rows
) {
    public string Id { get; } = id;

    public string TitleKey { get; } = titleKey;

    public SettingsRow[] Rows { get; } = rows;

    public SettingsEntry[] Entries { get; } = CollectEntries(rows);

    /// <summary>Gets whether any entry differs from its baseline.</summary>
    public bool IsDirty
    {
        get
        {
            for (var i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].IsDirty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Marks every entry's current value as its baseline.</summary>
    public void CaptureBaselines()
    {
        for (var i = 0; i < Entries.Length; i++)
        {
            Entries[i].CaptureBaseline();
        }
    }

    /// <summary>Restores every entry to its default value.</summary>
    public void ResetToDefaults()
    {
        for (var i = 0; i < Entries.Length; i++)
        {
            Entries[i].ResetToDefault();
        }
    }

    private static SettingsEntry[] CollectEntries(SettingsRow[] rows)
    {
        var entries = new List<SettingsEntry>();
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i] is SettingsEntry entry)
            {
                entries.Add(entry);
            }
        }

        return entries.ToArray();
    }
}
