using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Data;

internal abstract class Registry<TFileModel, TDefinition>(in ILogger logger, in IFileParseService fileParseService, in VirtualFileSystem vfs)
{
    protected readonly ILogger Logger = logger;
    protected readonly IFileParseService FileParseService = fileParseService;
    protected readonly VirtualFileSystem VFS = vfs;
    
    private readonly Dictionary<string, TDefinition> _definitions = [];
    
    public void Load()
    {
        List<PathInfo> files = VFS.GetFiles(GetDirectory(), SearchOption.AllDirectories).WhereToml().ToList();
        foreach (PathInfo file in files)
        {
            try
            {
                var fileModel = FileParseService.Parse<TFileModel>(file);
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
        
        foreach (KeyValuePair<string, TDefinition> definition in _definitions)
        {
            try
            {
                Result result = OnLoad(definition.Key, definition.Value);
                if (!result.Success)
                {
                    Logger.LogError(result, "Failed to load {type} \"{id}\".", typeof(TDefinition).Name, definition.Key);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load {type} \"{id}\".", typeof(TDefinition).Name, definition.Key);
            }
        }

        Logger.LogInformation("Registered {count} {type}s from {fileCount} files.", _definitions.Count, typeof(TDefinition).Name, files.Count);
    }
    
    protected abstract PathInfo GetDirectory();
    protected abstract IEnumerable<TDefinition> GetDefinitions(TFileModel model);
    protected abstract string GetID(TDefinition definition);

    protected virtual Result OnLoad(string id, TDefinition definition)
    {
        return Result.FromSuccess();
    }
}