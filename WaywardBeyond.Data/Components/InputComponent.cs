using Swordfish.ECS;

namespace WaywardBeyond.Data.Components;

/// <summary>
/// The client-authoritative input sample of an entity. The <see cref="ServerTickAtSample"/> stages
/// each sample on the server for the matching sim tick; the  client replays the same sequence for prediction.
/// </summary>
public partial struct InputComponent : IDataComponent;