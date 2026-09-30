using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Bodies;

/// <summary>
/// Provides headless access to body model definitions from virtual resources. Exposes each body as a
/// <see cref="BodyInfo"/> carrying its stable string ID and per-state directional texture paths. It carries
/// no render-coupled material types, so the client, the server, and headless consumers share one database.
/// </summary>
public sealed class BodyDatabase : VirtualAssetDatabase<BodyModels, BodyModel, BodyInfo>
{
    private readonly Dictionary<string, BodyModel> _models = [];

    /// <summary>The ID of the first loaded body, used as a default when a referenced body is unknown.</summary>
    public string? DefaultId { get; private set; }

    public BodyDatabase(
        in ILogger<BodyDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs
    ) : base(logger, fileParseService, vfs)
    {
        Load();
    }

    /// <summary>The number of loaded bodies.</summary>
    public int Count => _models.Count;

    /// <summary>The IDs of every loaded body, in insertion (parse) order.</summary>
    public IEnumerable<string> Ids => _models.Keys;

    /// <summary>
    /// Whether a body with the provided ID is loaded. Callers reference a body's string ID; this is the
    /// guard a client uses to fall back to a default body when an ID is unknown.
    /// </summary>
    public bool Contains(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        return _models.ContainsKey(id);
    }

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new PathInfo("bodies/");

    /// <inheritdoc/>
    protected override IEnumerable<BodyModel> GetAssetInfo(PathInfo path, BodyModels resource) => resource.Bodies ?? [];

    /// <inheritdoc/>
    protected override string GetAssetID(BodyModel assetInfo) => assetInfo.ID;

    /// <inheritdoc/>
    protected override Result<BodyInfo> LoadAsset(string id, BodyModel assetInfo)
    {
        var states = new Dictionary<string, string[]>();
        if (assetInfo.States != null)
        {
            foreach ((string stateTag, BodyStateTextures state) in assetInfo.States)
            {
                states[stateTag] = BodyDirectionOrder.Resolve(in state);
            }
        }

        var info = new BodyInfo(id, states);
        _models[id] = assetInfo;
        DefaultId ??= id;
        return Result<BodyInfo>.FromSuccess(info);
    }
}