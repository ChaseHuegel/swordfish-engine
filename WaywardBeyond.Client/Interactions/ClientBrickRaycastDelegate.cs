using Krill;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Interactions;

/// <summary>Raycasts a targeted voxel brick from screen space and builds its network interaction hint.</summary>
public delegate bool ClientBrickRaycastDelegate(
    bool offset,
    bool reachAround,
    out Entity entity,
    out Voxel voxel,
    out Int3 coordinate,
    out BrickInteraction hint
);
