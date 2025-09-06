using Swordfish.Library.Util;

namespace Swordfish.Library.Collections;

public interface IAssetDatabase<TAsset>
{
    /// <summary>
    ///     Attempts to get a <see cref="TAsset"/> by ID.
    /// </summary>
    Result<TAsset> Get(string id);
}
