using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Swordfish.IO;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Items;

internal sealed class ItemRegistry
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ItemDefinition> _definitions = [];
    
    public ItemRegistry(ILogger logger, IFileParseService fileParseService, VirtualFileSystem vfs)
    {
        _logger = logger;
        
        IEnumerable<PathInfo>? files = vfs.GetFiles(AssetPaths.Root.At("items"), SearchOption.AllDirectories).WhereToml();
        foreach (PathInfo file in files)
        {
            if (!fileParseService.TryParse(file, out ItemDefinitions definitions))
            {
                _logger.LogWarning("Failed to parse item definitions from \"{file}\".", file);
                continue;
            }

            foreach (ItemDefinition definition in definitions.Items)
            {
                _definitions[definition.ID] = definition;
            }
        }
    }
}