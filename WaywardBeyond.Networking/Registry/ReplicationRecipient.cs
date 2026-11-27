using Krill;

namespace WaywardBeyond.Networking.Registry;

/// <summary>Identifies the client receiving a replicated snapshot.</summary>
public readonly record struct ReplicationRecipient(Uuid ClientId, int Entity, Uuid EntityUuid);
