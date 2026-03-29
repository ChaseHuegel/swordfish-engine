using System.Collections.Generic;
using System.IO;
using System.Linq;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Services;

internal sealed class CharacterAssetService
{
    private readonly List<Material> _characterMaterials;
    
    public CharacterAssetService(in IAssetDatabase<Material> materialDatabase, in VirtualFileSystem vfs)
    {
        PathInfo characterMaterialsPath = AssetPaths.Materials.At("characters/");
        IEnumerable<string> characterMaterialIds = vfs.GetFiles(characterMaterialsPath, SearchOption.TopDirectoryOnly)
            .OrderBy(pathInfo => pathInfo.OriginalString, new NaturalComparer())
            .Select(pathInfo => $"characters/{pathInfo.GetFileNameWithoutExtension()}");

        _characterMaterials = [];
        foreach (string id in characterMaterialIds)
        {
            Result<Material> materialResult = materialDatabase.Get(id);
            if (materialResult)
            {
                _characterMaterials.Add(materialResult);
            }
        }
    }

    public int GetAppearancesCount()
    {
        return _characterMaterials.Count;
    }
    
    public Material GetAppearanceMaterial(int index)
    {
        return _characterMaterials[index];
    }

    public Material GetAppearanceMaterial(Character character)
    {
        return _characterMaterials[character.Body];
    }
}