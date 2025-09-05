using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        
        List<PathInfo> files = vfs.GetFiles(AssetPaths.Root.At("items"), SearchOption.AllDirectories).WhereToml().ToList();
        foreach (PathInfo file in files)
        {
            try
            {
                var definitions = fileParseService.Parse<ItemDefinitions>(file);
                foreach (ItemDefinition definition in definitions.Items)
                {
                    _definitions[definition.ID] = definition;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse item definitions from \"{file}\".", file);
            }
        }
        
        _logger.LogInformation("Registered {itemCount} item definitions from {fileCount} files.", _definitions.Count, files.Count);
    }
}