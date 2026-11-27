namespace Shoal.Modularity;

/// <summary>Controls which module capabilities are allowed within a module search root.</summary>
public enum ModuleRootPolicy
{
    /// <summary>Allows content assets and managed assemblies.</summary>
    Full,

    /// <summary>Allows content assets only and rejects manifests that declare assemblies.</summary>
    ContentOnly,
}
