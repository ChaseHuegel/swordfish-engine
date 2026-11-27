using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.Modularity;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;

namespace WaywardBeyond.Data.Packs;

/// <summary>
///     Reports content version skew for every loaded module that ships a <c>pack.toml</c>. Metadata is
///     advisory, so a mismatch or an unreadable file logs a warning and never blocks the load.
/// </summary>
internal sealed class ContentPackVersionChecker(ModuleCatalog moduleCatalog, ILogger<ContentPackVersionChecker> logger) : IEntryPoint
{
    /// <inheritdoc/>
    public void Run()
    {
        Check(moduleCatalog.Modules);
    }

    /// <summary>Checks the given loaded modules for content version skew.</summary>
    internal void Check(IReadOnlyList<LoadedModule> modules)
    {
        for (var i = 0; i < modules.Count; i++)
        {
            LoadedModule module = modules[i];
            PathInfo metadataPath = module.Root.At(ContentPackMetadata.FILE_NAME);
            if (!metadataPath.FileExists())
            {
                continue;
            }

            ContentPackMetadata metadata;
            try
            {
                metadata = Toml.To<ContentPackMetadata>(metadataPath.ReadString());
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Content pack \"{id}\" has an unreadable pack.toml at \"{path}\". The pack loads without a version check.",
                    module.ID,
                    metadataPath.Value);
                continue;
            }

            if (metadata.ContentVersion == ContentVersion.CURRENT)
            {
                continue;
            }

            logger.LogWarning(
                "Content pack \"{id}\" targets ContentVersion {packVersion}, but this build expects {currentVersion}. The pack still loads.",
                module.ID,
                metadata.ContentVersion,
                ContentVersion.CURRENT);
        }
    }
}
