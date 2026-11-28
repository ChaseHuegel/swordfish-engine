using System.Threading;
using Krill;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Server.Systems;

/// <summary>Broadcasts newly spawned server voxel structures to every connected client in a world.</summary>
internal static class ServerVoxelEntityBroadcaster
{
    private static uint _nextBatchId;

    /// <summary>Fragments <paramref name="entityData"/> and sends <see cref="VoxelEntitySpawnPart"/> messages to all world peers.</summary>
    public static void BroadcastEntitySpawn(
        DataStore store,
        SessionManager sessions,
        ServerPeers peers,
        ILogger? logger,
        in VoxelEntityData entityData
    )
    {
        byte[] payload = entityData.Serialize();
        uint batchId = Interlocked.Increment(ref _nextBatchId);
        int partCount = PayloadFragmenter.PartCount(payload.Length);

        foreach (Uuid clientId in sessions.Clients)
        {
            if (!peers.TryGet(clientId, out ServerPeer peer) || peer.World?.Store != store)
            {
                continue;
            }

            for (var partIndex = 0; partIndex < partCount; partIndex++)
            {
                var part = new VoxelEntitySpawnPart
                {
                    BatchId = batchId,
                    PartIndex = (uint)partIndex,
                    PartCount = (uint)partCount,
                    Payload = PayloadFragmenter.Slice(payload, partIndex),
                };
                if (!peer.Client.VoxelEntitySpawn(part))
                {
                    if (logger != null)
                    {
                        peer.CloseOnRejectedSend(logger);
                    }
                    else
                    {
                        peer.Rpc.Close();
                    }

                    break;
                }
            }
        }
    }
}
