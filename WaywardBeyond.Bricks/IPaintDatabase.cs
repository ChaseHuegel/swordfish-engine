using System;
using System.Collections.Generic;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;

namespace WaywardBeyond.Bricks;

/// <summary>Provides access to all loaded <see cref="Paint"/> assets.</summary>
public interface IPaintDatabase : IAssetDatabase<Paint>
{
    /// <summary>All registered texture paints in registry order.</summary>
    IReadOnlyList<Paint> TexturePaints { get; }

    /// <summary>All registered color swatch paints in registry order.</summary>
    IReadOnlyList<Paint> ColorPaints { get; }

    /// <summary>Attempts to get a paint by its data id.</summary>
    Result<Paint> Get(ushort id);

    /// <summary>Returns all paints matching a predicate.</summary>
    List<Paint> Get(Func<Paint, bool> predicate);
}
