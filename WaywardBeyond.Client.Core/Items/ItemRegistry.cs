using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Data;
using Material = glTFLoader.Schema.Material;

namespace WaywardBeyond.Client.Core.Items;

internal sealed class ItemRegistry : Registry<ItemDefinitions, ItemDefinition>
{
    private readonly Shader _iconShader;
    private readonly Dictionary<string, Material> _materials = [];

    public ItemRegistry(in ILogger<ItemRegistry> logger, in IFileParseService fileParseService, in VirtualFileSystem vfs) 
        : base(logger, fileParseService, vfs)
    {
        _iconShader = FileParseService.Parse<Shader>(AssetPaths.Shaders.At("ui_reef_textured.glsl"));
    }
    
    protected override PathInfo GetDirectory() => AssetPaths.Root.At("items");
    protected override IEnumerable<ItemDefinition> GetDefinitions(ItemDefinitions model) => model.Items;
    protected override string GetID(ItemDefinition definition) => definition.ID;

    protected override Result OnLoad(string id, ItemDefinition definition)
    {
        base.OnLoad(id, definition);
        
        return Result.FromSuccess();
    }
}