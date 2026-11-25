using System.Numerics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Lightweight brick database and registry built from a loaded workspace for real-time 3D preview meshing.
/// </summary>
public sealed class StudioPreviewBrickDatabase : IBrickDatabase, IBrickRegistry
{
    private readonly Lock _syncLock = new();
    private readonly Dictionary<string, ushort> _idByName = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, Brick> _brickById = [];
    private readonly Dictionary<string, ushort> _extensionIdByName = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, string> _extensionNameById = [];
    private ushort _nextId = Brick.MIN_ID;

    /// <summary>
    ///     Populates a preview brick database from all winning brick assets in <paramref name="workspace"/>.
    /// </summary>
    public static StudioPreviewBrickDatabase FromWorkspace(ToolsWorkspace.WorkspaceIndex? workspace)
    {
        var db = new StudioPreviewBrickDatabase();
        if (workspace == null)
        {
            return db;
        }

        IReadOnlyList<ToolsWorkspace.IndexedAsset> bricks = workspace.GetWinningAssetsByKind(ToolsWorkspace.AssetKind.Brick);
        List<BrickDefinition> definitions = [];
        for (var i = 0; i < bricks.Count; i++)
        {
            if (bricks[i].AuthoritativeDefinition is BrickDefinition def && !string.IsNullOrWhiteSpace(def.ID))
            {
                definitions.Add(def);
            }
        }

        definitions.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.ID, right.ID));
        for (var i = 0; i < definitions.Count; i++)
        {
            db.RegisterOrUpdate(BuildRuntimeBrick(definitions[i]));
        }

        return db;
    }

    /// <summary>
    ///     Registers a new preview brick or updates an existing brick entry in place.
    /// </summary>
    public Brick RegisterOrUpdate(Brick brick)
    {
        if (brick == null || string.IsNullOrWhiteSpace(brick.ID))
        {
            return brick!;
        }

        lock (_syncLock)
        {
            if (!_idByName.TryGetValue(brick.ID, out ushort id))
            {
                if (_extensionIdByName.Remove(brick.ID, out ushort extensionId))
                {
                    _extensionNameById.Remove(extensionId);
                    id = extensionId;
                    _idByName[brick.ID] = id;
                }
                else
                {
                    if (_nextId == ushort.MaxValue)
                    {
                        return brick;
                    }

                    id = _nextId++;
                    _idByName[brick.ID] = id;
                }
            }

            _brickById[id] = brick;
            return brick;
        }
    }

    /// <summary>
    ///     Converts a parsed <see cref="BrickDefinition"/> into a runtime <see cref="Brick"/> for meshing.
    /// </summary>
    public static Brick BuildRuntimeBrick(in BrickDefinition def)
    {
        var color = Vector3.One;
        if (!string.IsNullOrWhiteSpace(def.Color) && HexColor.TryParseRgb(def.Color, out Vector3 parsed))
        {
            color = parsed;
        }

        return new Brick(
            def.ID,
            def.Alpha,
            def.Passable,
            def.Mesh,
            def.Shape,
            def.Textures,
            def.CastShadows,
            color: color);
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
        if (id < Brick.MIN_ID)
        {
            return null;
        }

        lock (_syncLock)
        {
            if (_brickById.TryGetValue(id, out Brick? brick))
            {
                return brick.ID;
            }

            return _extensionNameById.TryGetValue(id, out string? extensionName) ? extensionName : null;
        }
    }

    /// <inheritdoc/>
    public Result<Brick> Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Result<Brick>.FromFailure("Brick identifier is empty.");
        }

        lock (_syncLock)
        {
            return _idByName.TryGetValue(id, out ushort dataId) && _brickById.TryGetValue(dataId, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure($"Unknown brick '{id}'.");
        }
    }

    /// <inheritdoc/>
    public Result<Brick> Get(ushort id)
    {
        lock (_syncLock)
        {
            return _brickById.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure($"Unknown brick '{id}'.");
        }
    }

    /// <inheritdoc/>
    public List<Brick> Get(Func<Brick, bool> predicate)
    {
        lock (_syncLock)
        {
            return [.. _brickById.Values.Where(predicate)];
        }
    }

    /// <inheritdoc/>
    public bool IsCuller(in Voxel voxel, BrickShape shape)
    {
        if (voxel.ID == 0 || shape != BrickShape.Block)
        {
            return false;
        }

        lock (_syncLock)
        {
            return _brickById.TryGetValue(voxel.ID, out Brick? brick)
                && !brick.Passable
                && brick.Alpha == BrickAlpha.Opaque
                && brick.CastShadows;
        }
    }
}
