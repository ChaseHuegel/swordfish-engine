using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.IO;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Data;

namespace WaywardBeyond.Client.Core.Items;

internal sealed class ItemRegistry(
    in ILogger<ItemRegistry> logger,
    in IFileParseService fileParseService,
    in VirtualFileSystem vfs)
    : Registry<ItemDefinitions, ItemDefinition>(logger, fileParseService, vfs)
{
    protected override PathInfo GetDirectory() => AssetPaths.Root.At("items");
    protected override IEnumerable<ItemDefinition> GetDefinitions(ItemDefinitions model) => model.Items;
    protected override string GetID(ItemDefinition definition) => definition.ID;
}