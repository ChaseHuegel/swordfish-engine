using Swordfish.ECS;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking.Components;

/// <summary>
/// The client-authoritative movement/look input of a player, replicated upstream per sample. The
/// <see cref="ServerTickAtSample"/> key stages each sample on the server for the matching sim tick; the
/// client replays the same sequence for prediction (see <see cref="PendingInputComponent"/>).
/// </summary>
[NetworkComponent(1, NetworkDirection.ClientOwned)]
public partial struct InputComponent : IDataComponent;