using System.Numerics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Lightweight paint database and registry built from a loaded workspace for real-time 3D preview meshing.
/// </summary>
public sealed class StudioPreviewPaintDatabase : IPaintDatabase, IPaintRegistry
{
    private readonly Lock _syncLock = new();
    private readonly Dictionary<string, ushort> _idByName = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, Paint> _paintById = [];
    private readonly Dictionary<string, ushort> _extensionIdByName = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, string> _extensionNameById = [];
    private readonly List<Paint> _texturePaints = [];
    private readonly List<Paint> _colorPaints = [];
    private ushort _nextId = Paint.MIN_ID;

    /// <summary>
    ///     Populates a preview paint database from all winning paint assets in <paramref name="workspace"/>.
    /// </summary>
    public static StudioPreviewPaintDatabase FromWorkspace(ToolsWorkspace.WorkspaceIndex? workspace)
    {
        var db = new StudioPreviewPaintDatabase();
        if (workspace == null)
        {
            return db;
        }

        IReadOnlyList<ToolsWorkspace.IndexedAsset> assets = workspace.GetWinningAssetsByKind(ToolsWorkspace.AssetKind.Paint);
        List<PaintDefinition> definitions = [];
        for (var i = 0; i < assets.Count; i++)
        {
            if (assets[i].AuthoritativeDefinition is PaintDefinition def && !string.IsNullOrWhiteSpace(def.ID))
            {
                definitions.Add(def);
            }
        }

        definitions.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.ID, right.ID));
        for (var i = 0; i < definitions.Count; i++)
        {
            Paint? paint = BuildRuntimePaint(definitions[i]);
            if (paint != null)
            {
                db.RegisterOrUpdate(paint);
            }
        }

        return db;
    }

    /// <summary>
    ///     Converts a parsed <see cref="PaintDefinition"/> into a runtime <see cref="Paint"/> for meshing, or null when invalid.
    /// </summary>
    public static Paint? BuildRuntimePaint(in PaintDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.ID))
        {
            return null;
        }

        if (HasNonEmptyTexture(def.Textures))
        {
            return Paint.CreateTexture(def.ID, def.Name, def.Alpha, def.Connected, def.Textures!);
        }

        if (!string.IsNullOrWhiteSpace(def.Color) && HexColor.TryParseRgb(def.Color, out Vector3 rgb))
        {
            return Paint.CreateColor(def.ID, def.Name, rgb);
        }

        return null;
    }

    /// <summary>
    ///     Registers a new preview paint or updates an existing paint entry in place.
    /// </summary>
    public Paint RegisterOrUpdate(Paint paint)
    {
        if (paint == null || string.IsNullOrWhiteSpace(paint.ID))
        {
            return paint!;
        }

        lock (_syncLock)
        {
            if (!_idByName.TryGetValue(paint.ID, out ushort id))
            {
                if (_extensionIdByName.Remove(paint.ID, out ushort extensionId))
                {
                    _extensionNameById.Remove(extensionId);
                    id = extensionId;
                    _idByName[paint.ID] = id;
                }
                else
                {
                    if (_nextId == ushort.MaxValue)
                    {
                        return paint;
                    }

                    id = _nextId++;
                    _idByName[paint.ID] = id;
                }
            }

            _paintById[id] = paint;
            UpdateCategoryLists(paint);
            return paint;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Paint> TexturePaints
    {
        get
        {
            lock (_syncLock)
            {
                return [.. _texturePaints];
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Paint> ColorPaints
    {
        get
        {
            lock (_syncLock)
            {
                return [.. _colorPaints];
            }
        }
    }

    /// <inheritdoc/>
    public ushort Id(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }

        lock (_syncLock)
        {
            return _idByName.TryGetValue(name, out ushort id) ? id : (ushort)0;
        }
    }

    /// <inheritdoc/>
    public ushort Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        lock (_syncLock)
        {
            if (_idByName.TryGetValue(name, out ushort knownId))
            {
                return knownId;
            }

            if (!name.Contains(':', StringComparison.Ordinal) && _idByName.TryGetValue($"wb:{name}", out ushort prefixedId))
            {
                return prefixedId;
            }

            if (_extensionIdByName.TryGetValue(name, out ushort existingId))
            {
                return existingId;
            }

            if (_nextId == ushort.MaxValue)
            {
                return 0;
            }

            ushort allocated = _nextId++;
            _extensionIdByName[name] = allocated;
            _extensionNameById[allocated] = name;
            return allocated;
        }
    }

    /// <inheritdoc/>
    public string? Name(ushort id)
    {
        if (id < Paint.MIN_ID)
        {
            return null;
        }

        lock (_syncLock)
        {
            if (_paintById.TryGetValue(id, out Paint? paint))
            {
                return paint.ID;
            }

            return _extensionNameById.TryGetValue(id, out string? extensionName) ? extensionName : null;
        }
    }

    /// <inheritdoc/>
    public Result<Paint> Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Result<Paint>.FromFailure("Paint identifier is empty.");
        }

        lock (_syncLock)
        {
            return _idByName.TryGetValue(id, out ushort dataId) && _paintById.TryGetValue(dataId, out Paint? paint)
                ? Result<Paint>.FromSuccess(paint)
                : Result<Paint>.FromFailure($"Unknown paint '{id}'.");
        }
    }

    /// <inheritdoc/>
    public Result<Paint> Get(ushort id)
    {
        lock (_syncLock)
        {
            return _paintById.TryGetValue(id, out Paint? paint)
                ? Result<Paint>.FromSuccess(paint)
                : Result<Paint>.FromFailure($"Unknown paint '{id}'.");
        }
    }

    /// <inheritdoc/>
    public List<Paint> Get(Func<Paint, bool> predicate)
    {
        lock (_syncLock)
        {
            return [.. _paintById.Values.Where(predicate)];
        }
    }

    private void UpdateCategoryLists(Paint paint)
    {
        RemoveById(_texturePaints, paint.ID);
        RemoveById(_colorPaints, paint.ID);

        if (paint.Kind == PaintKind.Texture)
        {
            _texturePaints.Add(paint);
        }
        else
        {
            _colorPaints.Add(paint);
        }
    }

    private static void RemoveById(List<Paint> list, string id)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i].ID, id, StringComparison.Ordinal))
            {
                list.RemoveAt(i);
            }
        }
    }

    private static bool HasNonEmptyTexture(string?[]? textures)
    {
        if (textures == null || textures.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < textures.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(textures[i]))
            {
                return true;
            }
        }

        return false;
    }
}
