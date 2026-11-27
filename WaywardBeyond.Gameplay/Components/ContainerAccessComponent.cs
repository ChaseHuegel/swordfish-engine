using Krill;
using Swordfish.Library.Util;

namespace WaywardBeyond.Gameplay.Components;

/// <summary>Server-side access control and reach limits for an interactive container inventory.</summary>
public struct ContainerAccessComponent : IDataComponent
{
    /// <summary>The owning entity UUID, or default when unowned.</summary>
    public Uuid Owner;

    /// <summary>The entity UUID currently permitted exclusive viewer access, or default when shared.</summary>
    public Uuid ActiveViewer;

    /// <summary>Whether non-owners are blocked from accessing this container.</summary>
    public bool IsLocked;

    /// <summary>Maximum distance from the acting player in meters; non-positive uses the default reach.</summary>
    public float MaxReach;

    /// <summary>Optional permission node required to access this container.</summary>
    public string? RequiredPermission;
}
