using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.AssetStudio.ViewModels;
using WaywardBeyond.Bodies;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>Resolves deterministic workspace reference assets such as the standing Body scale sprite.</summary>
public static class WorkspaceModelReferenceResolver
{
    private static readonly string[] CANONICAL_DIRECTIONS = ["front", "back", "left", "right"];

    /// <summary>Selects the first body ordered by ID that resolves to a valid existing sprite image on disk regardless of state.</summary>
    public static BodyReferenceSpriteInfo? ResolveDeterministicBodySprite(
        ToolsWorkspace.WorkspaceIndex? workspace,
        IReadOnlyList<AssetDocumentViewModel>? documents,
        Func<string?, string?> resolveTexturePath)
    {
        ArgumentNullException.ThrowIfNull(resolveTexturePath);

        if (workspace != null)
        {
            IReadOnlyList<ToolsWorkspace.IndexedAsset> bodies = workspace.GetWinningAssetsByKind(ToolsWorkspace.AssetKind.Body);
            if (bodies.Count > 0)
            {
                var orderedBodies = bodies
                    .Where(static a => a.AuthoritativeDefinition is BodyDefinition)
                    .OrderBy(static a => a.Id, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static a => a.Id, StringComparer.Ordinal)
                    .ToList();

                for (var i = 0; i < orderedBodies.Count; i++)
                {
                    var bodyDef = (BodyDefinition)orderedBodies[i].AuthoritativeDefinition!;
                    BodyReferenceSpriteInfo? hit = TryResolveFromBodyDefinition(orderedBodies[i].Id, in bodyDef, resolveTexturePath);
                    if (hit.HasValue)
                    {
                        return hit;
                    }
                }
            }
        }

        if (documents == null || documents.Count == 0)
        {
            return null;
        }

        var orderedDocs = documents
            .Where(static d => d.Kind == AssetKind.Body)
            .OrderBy(static d => d.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static d => d.Id, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < orderedDocs.Count; i++)
        {
            BodyReferenceSpriteInfo? hit = TryResolveFromBodyDocument(orderedDocs[i], resolveTexturePath);
            if (hit.HasValue)
            {
                return hit;
            }
        }

        return null;
    }

    private static BodyReferenceSpriteInfo? TryResolveFromBodyDefinition(
        string bodyId,
        in BodyDefinition body,
        Func<string?, string?> resolveTexturePath)
    {
        if (body.States == null || body.States.Count == 0)
        {
            return null;
        }

        var orderedStates = OrderStateKeys(body.States.Keys);
        for (var s = 0; s < orderedStates.Count; s++)
        {
            string stateName = orderedStates[s];
            if (!TryGetCaseInsensitive(body.States, stateName, out Dictionary<string, string?[]>? dirMap) || dirMap == null)
            {
                continue;
            }

            List<string> orderedDirs = OrderDirectionKeys(dirMap.Keys);
            for (var d = 0; d < orderedDirs.Count; d++)
            {
                string dir = orderedDirs[d];
                if (!TryGetCaseInsensitive(dirMap, dir, out string?[]? paths) || paths == null)
                {
                    continue;
                }

                for (var p = 0; p < paths.Length; p++)
                {
                    if (TryLoadCandidate(bodyId, stateName, dir, paths[p], resolveTexturePath, out BodyReferenceSpriteInfo info))
                    {
                        return info;
                    }
                }
            }
        }

        return null;
    }

    private static BodyReferenceSpriteInfo? TryResolveFromBodyDocument(
        AssetDocumentViewModel doc,
        Func<string?, string?> resolveTexturePath)
    {
        if (doc.AllBodySprites.Count == 0)
        {
            return null;
        }

        var orderedStates = OrderStateKeys(doc.BodyStates.Count > 0
            ? doc.BodyStates
            : doc.AllBodySprites.Select(static s => s.StateName).Distinct(StringComparer.OrdinalIgnoreCase));

        for (var s = 0; s < orderedStates.Count; s++)
        {
            string stateName = orderedStates[s];
            var slotsForState = doc.AllBodySprites
                .Where(slot => string.Equals(slot.StateName, stateName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var orderedDirs = OrderDirectionKeys(slotsForState.Select(static slot => slot.Direction));
            for (var d = 0; d < orderedDirs.Count; d++)
            {
                string dir = orderedDirs[d];
                for (var k = 0; k < slotsForState.Count; k++)
                {
                    BodySpriteSlotViewModel slot = slotsForState[k];
                    if (!string.Equals(slot.Direction, dir, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string?[] paths =
                    [
                        slot.ResolvedAbsolutePath,
                        .. slot.TexturePath.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    ];
                    for (var p = 0; p < paths.Length; p++)
                    {
                        if (TryLoadCandidate(doc.Id, stateName, dir, paths[p], resolveTexturePath, out BodyReferenceSpriteInfo info))
                        {
                            return info;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool TryLoadCandidate(
        string bodyId,
        string stateName,
        string direction,
        string? rawPath,
        Func<string?, string?> resolveTexturePath,
        out BodyReferenceSpriteInfo info)
    {
        info = default;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        string trimmed = rawPath.Trim();
        string? absPath = File.Exists(trimmed)
            ? Path.GetFullPath(trimmed)
            : resolveTexturePath(trimmed);
        if (string.IsNullOrWhiteSpace(absPath) || !File.Exists(absPath))
        {
            return false;
        }

        Result<StandaloneTextureData> loaded = StandaloneTextureLoader.LoadFromFile(absPath);
        if (!loaded.Success || !loaded.Value.IsValid)
        {
            return false;
        }

        info = new BodyReferenceSpriteInfo(bodyId, stateName, direction, absPath, loaded.Value);
        return true;
    }

    private static List<string> OrderStateKeys(IEnumerable<string> keys)
    {
        return keys
            .Where(static k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static k => string.Equals(k, "standing", StringComparison.OrdinalIgnoreCase))
            .ThenBy(static k => k, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static k => k, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> OrderDirectionKeys(IEnumerable<string> keys)
    {
        return keys
            .Where(static k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static k =>
            {
                for (var i = 0; i < CANONICAL_DIRECTIONS.Length; i++)
                {
                    if (string.Equals(k, CANONICAL_DIRECTIONS[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return CANONICAL_DIRECTIONS.Length;
            })
            .ThenBy(static k => k, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static k => k, StringComparer.Ordinal)
            .ToList();
    }

    private static bool TryGetCaseInsensitive<T>(Dictionary<string, T> dict, string key, out T? value)
    {
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (KeyValuePair<string, T> kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
