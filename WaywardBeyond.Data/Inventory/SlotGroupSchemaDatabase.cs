using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Data;

/// <summary>Loads data-driven <see cref="SlotGroupSchema"/> definitions and provides built-in canonical defaults.</summary>
public sealed class SlotGroupSchemaDatabase : ISlotGroupSchemaDatabase
{
    private const string VIRTUAL_ROOT = "inventory/";

    private readonly FrozenDictionary<string, SlotGroupSchema> _schemas;

    /// <summary>Shared default schema database containing the built-in slot groups.</summary>
    public static ISlotGroupSchemaDatabase Default { get; } = new SlotGroupSchemaDatabase();

    /// <summary>Constructs a schema database seeded with built-in defaults and optional VFS TOML definitions.</summary>
    public SlotGroupSchemaDatabase(
        ILogger<SlotGroupSchemaDatabase>? logger = null,
        IFileParseService? fileParseService = null,
        VirtualFileSystem? vfs = null
    )
    {
        var schemas = new Dictionary<string, SlotGroupSchema>(StringComparer.Ordinal);
        SeedDefaults(schemas);

        if (fileParseService != null && vfs != null)
        {
            LoadFromVfs(schemas, logger, fileParseService, vfs);
        }

        _schemas = schemas.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public Result<SlotGroupSchema> Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Result<SlotGroupSchema>.FromFailure("Slot group ID cannot be empty.");
        }

        return _schemas.TryGetValue(id, out SlotGroupSchema? schema)
            ? Result<SlotGroupSchema>.FromSuccess(schema)
            : Result<SlotGroupSchema>.FromFailure($"Unknown slot group schema \"{id}\".");
    }

    /// <inheritdoc/>
    public bool AllowsItem(in SlotId slot, string? itemId, LocalizedTags? tags)
    {
        if (string.IsNullOrEmpty(slot.GroupId) || slot.LocalIndex < 0 || string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        //  Unregistered custom groups stay unrestricted unless a schema defines filter tags.
        return !_schemas.TryGetValue(slot.GroupId, out SlotGroupSchema? schema)
            || schema.AllowsItem(slot.LocalIndex, itemId, tags);
    }

    /// <inheritdoc/>
    public int GetCapacity(string groupId, int fallback = 0)
    {
        if (string.IsNullOrEmpty(groupId))
        {
            return fallback;
        }

        return _schemas.TryGetValue(groupId, out SlotGroupSchema? schema)
            ? schema.DefaultCapacity
            : fallback;
    }

    private static void SeedDefaults(Dictionary<string, SlotGroupSchema> schemas)
    {
        Register(schemas, new SlotGroupSchema(SlotGroups.HOTBAR, SlotGroups.DEFAULT_HOTBAR_CAPACITY));
        Register(schemas, new SlotGroupSchema(SlotGroups.BACKPACK, SlotGroups.DEFAULT_BACKPACK_CAPACITY));
        Register(schemas, new SlotGroupSchema(
            SlotGroups.WEARABLES,
            SlotGroups.DEFAULT_WEARABLES_CAPACITY,
            allowedTags: ["item.wearable"],
            slotAllowedTags: new Dictionary<int, string[]>
            {
                [0] = ["item.wearable.helmet"],
                [1] = ["item.wearable.torso"],
                [2] = ["item.wearable.legs"],
                [3] = ["item.wearable.head"],
                [4] = ["item.wearable.suit"],
                [5] = ["item.wearable.o2"],
            }));
        Register(schemas, new SlotGroupSchema(
            SlotGroups.AUGMENTS,
            SlotGroups.DEFAULT_AUGMENTS_CAPACITY,
            allowedTags: ["item.augment"]));
        Register(schemas, new SlotGroupSchema(SlotGroups.INPUT, 1));
        Register(schemas, new SlotGroupSchema(SlotGroups.OUTPUT, 1));
        Register(schemas, new SlotGroupSchema(SlotGroups.FUEL, 1, allowedTags: ["item.fuel"]));
        Register(schemas, new SlotGroupSchema(SlotGroups.CARGO, SlotGroups.DEFAULT_CARGO_CAPACITY));
    }

    private static void Register(Dictionary<string, SlotGroupSchema> schemas, SlotGroupSchema schema)
    {
        schemas[schema.GroupId] = schema;
    }

    private static void LoadFromVfs(
        Dictionary<string, SlotGroupSchema> schemas,
        ILogger<SlotGroupSchemaDatabase>? logger,
        IFileParseService fileParseService,
        VirtualFileSystem vfs
    )
    {
        PathInfo[] files = vfs.GetFiles(new PathInfo(VIRTUAL_ROOT), SearchOption.AllDirectories);

        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            PathInfo file = files[fileIndex];
            if (!file.HasExtension(".toml"))
            {
                continue;
            }

            try
            {
                var definitions = fileParseService.Parse<SlotGroupDefinitions>(file);
                if (definitions.SlotGroups == null)
                {
                    continue;
                }

                for (var i = 0; i < definitions.SlotGroups.Count; i++)
                {
                    SlotGroupDefinition def = definitions.SlotGroups[i];
                    string id = def.ID?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(id) || def.Capacity < 0)
                    {
                        logger?.LogWarning("Skipping invalid slot group schema in \"{file}\".", file);
                        continue;
                    }

                    var slotFilters = new Dictionary<int, string[]>();
                    if (def.Slots != null)
                    {
                        for (var j = 0; j < def.Slots.Count; j++)
                        {
                            SlotFilterDefinition slotDef = def.Slots[j];
                            if (slotDef.Index >= 0 && slotDef.AllowedTags != null)
                            {
                                slotFilters[slotDef.Index] = [.. slotDef.AllowedTags];
                            }
                        }
                    }

                    string[]? groupTags = def.AllowedTags != null ? [.. def.AllowedTags] : null;
                    schemas[id] = new SlotGroupSchema(id, def.Capacity, groupTags, slotFilters);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load slot group schemas from \"{file}\".", file);
            }
        }
    }
}
