using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Data;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickRegistry(
    in ILogger<BrickRegistry> logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : Registry<BrickDefinitions, BrickDefinition>(logger, fileParseService, vfs)
{
    protected override PathInfo GetDirectory() => AssetPaths.Root.At("bricks");
    protected override IEnumerable<BrickDefinition> GetDefinitions(BrickDefinitions model) => model.Bricks;
    protected override string GetID(BrickDefinition definition) => definition.ID;
}