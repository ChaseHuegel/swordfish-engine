using System.Collections.Generic;
using DryIoc;
using Shoal.Extensions.Swordfish;

namespace WaywardBeyond.Permissions;

/// <summary>The permissions project's composition entry.</summary>
public static class PermissionsComposition
{
    /// <summary>Registers the permission file parser, the loader, and the compiled policy.</summary>
    public static void RegisterPermissions(this IContainer container)
    {
        container.RegisterTomlParser<PermissionFile>();
        container.Register<PermissionFileLoader>(Reuse.Singleton);
        container.RegisterDelegate<PermissionPolicy>(context =>
        {
            PermissionFileLoader loader = context.Resolve<PermissionFileLoader>();
            var diagnostics = new PermissionDiagnostics();
            IReadOnlyList<SourcedPermissionFile> files = loader.Load(diagnostics);
            return PermissionPolicy.Create(files, diagnostics);
        }, Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
    }
}
