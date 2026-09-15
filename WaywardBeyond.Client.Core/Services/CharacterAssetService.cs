using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Services;

internal sealed class CharacterAssetService
{
    private const string FLOATING_SUFFIX = "_floating";

    private readonly List<string> _characterMaterialIds;
    private readonly List<Material> _characterMaterials;
    private readonly IAssetDatabase<Material> _materialDatabase;

    public CharacterAssetService(in IAssetDatabase<Material> materialDatabase, in VirtualFileSystem vfs)
    {
        _materialDatabase = materialDatabase;

        PathInfo characterMaterialsPath = AssetPaths.Materials.At("characters/");
        IEnumerable<string> standingMaterialIds = vfs.GetFiles(characterMaterialsPath, SearchOption.TopDirectoryOnly)
            .OrderBy(pathInfo => pathInfo.OriginalString, new NaturalComparer())
            .Select(pathInfo => $"characters/{pathInfo.GetFileNameWithoutExtension()}")
            .Where(id => !id.EndsWith(FLOATING_SUFFIX, StringComparison.Ordinal));

        _characterMaterialIds = [];
        _characterMaterials = [];
        foreach (string id in standingMaterialIds)
        {
            Result<Material> materialResult = materialDatabase.Get(id);
            if (materialResult)
            {
                _characterMaterialIds.Add(id);
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
        return GetAppearanceMaterial(index, CharacterAssetVariant.Standing);
    }

    public Material GetAppearanceMaterial(int index, CharacterAssetVariant variant)
    {
        if (variant == CharacterAssetVariant.Floating)
        {
            //  Not every body has a floating variant; fall back to the standing material when it does not.
            Result<Material> floatingResult = _materialDatabase.Get(_characterMaterialIds[index] + FLOATING_SUFFIX);
            if (floatingResult)
            {
                return floatingResult;
            }
        }

        return _characterMaterials[index];
    }

    public Material GetAppearanceMaterial(Character character)
    {
        return GetAppearanceMaterial(character.Body);
    }

    public Material GetAppearanceMaterial(Character character, CharacterAssetVariant variant)
    {
        return GetAppearanceMaterial(character.Body, variant);
    }
}