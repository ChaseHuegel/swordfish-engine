using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Configuration;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Configuration;

/// <summary>Persists non-default player keybinding overrides to <c>keybindings.toml</c>.</summary>
public sealed class KeybindingSettings : Config<KeybindingSettings>
{
    /// <summary>Serialized token representing an intentionally cleared keybinding.</summary>
    public const string UNBOUND_TOKEN = "Unbound";

    /// <summary>Map of shortcut identifier to serialized binding string.</summary>
    public Dictionary<string, string> Bindings { get; private set; } = new(StringComparer.Ordinal);

    /// <summary>Parses valid binding overrides from <see cref="Bindings"/>, skipping malformed entries.</summary>
    public Dictionary<string, ShortcutBinding> ParseBindings(ILogger? logger = null)
    {
        var parsed = new Dictionary<string, ShortcutBinding>(StringComparer.Ordinal);
        if (Bindings == null)
        {
            return parsed;
        }

        foreach ((string id, string raw) in Bindings)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (string.Equals(raw.Trim(), UNBOUND_TOKEN, StringComparison.OrdinalIgnoreCase))
            {
                parsed[id] = ShortcutBinding.Unbound;
                continue;
            }

            if (ShortcutBinding.TryParse(raw, out ShortcutBinding binding))
            {
                parsed[id] = binding;
            }
            else
            {
                logger?.LogWarning("Ignoring invalid keybinding override \"{raw}\" for \"{id}\".", raw, id);
            }
        }

        return parsed;
    }

    /// <summary>Replaces <see cref="Bindings"/> with sorted configuration entries for <paramref name="overrides"/>.</summary>
    public void SetOverrides(IReadOnlyDictionary<string, ShortcutBinding> overrides)
    {
        Bindings ??= new Dictionary<string, string>(StringComparer.Ordinal);
        Bindings.Clear();

        var ids = new List<string>(overrides.Keys);
        ids.Sort(StringComparer.Ordinal);
        for (var i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            ShortcutBinding binding = overrides[id];
            Bindings[id] = binding.IsBound ? binding.ToConfigString() : UNBOUND_TOKEN;
        }
    }
}
