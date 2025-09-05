using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core.Data;

internal abstract class Registry<TFileModel, TDefinition>(in ILogger logger, in IFileParseService fileParseService, in VirtualFileSystem vfs)
{
    protected readonly ILogger Logger = logger;
    
    private readonly IFileParseService _fileParseService = fileParseService;
    private readonly VirtualFileSystem _vfs = vfs;
    private readonly Dictionary<string, TDefinition> _definitions = [];
    
    public void Load()
    {
        List<PathInfo> files = _vfs.GetFiles(GetDirectory(), SearchOption.AllDirectories).WhereToml().ToList();
        foreach (PathInfo file in files)
        {
            try
            {
                var fileModel = _fileParseService.Parse<TFileModel>(file);
                foreach (TDefinition definition in GetDefinitions(fileModel))
                {
                    _definitions[GetID(definition)] = definition;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to parse {type} from \"{file}\".", typeof(TDefinition).Name, file);
            }
        }
        
        Logger.LogInformation("Registered {count} {type}s from {fileCount} files.", _definitions.Count, typeof(TDefinition).Name, files.Count);
    }
    
    protected abstract PathInfo GetDirectory();
    protected abstract IEnumerable<TDefinition> GetDefinitions(TFileModel model);
    protected abstract string GetID(TDefinition definition);
}