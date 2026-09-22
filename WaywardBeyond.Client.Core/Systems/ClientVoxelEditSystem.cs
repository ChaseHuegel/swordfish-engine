using System;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels.Building;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Applies authoritative <see cref="VoxelEditMessage"/>s from the server onto the client's world through
/// the same shared apply path the server used to author them (<c>VoxelObject.Set</c> + a mesh rebuild),
/// so both the origin client and remote witnesses converge on the authoritative voxel state without any
/// client-side authority logic. This is the base apply; prediction-confirm/revert correlation is layered
/// on in a later phase.
/// </summary>
internal sealed class ClientVoxelEditSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly Func<VoxelEntityBuilder> _voxelBuilder;

    private VoxelEntityBuilder? _resolvedBuilder;

    public ClientVoxelEditSystem(
        in IClientConnection transport,
        in Func<VoxelEntityBuilder> voxelBuilder
    ) {
        _transport = transport;
        _voxelBuilder = voxelBuilder;
    }

    public void Tick(float delta, DataStore store)
    {
        //  Authoritative edits only apply once the world is built and play has begun; during Loading the
        //  load thread builds view entities and writing into those entities concurrently would race it.
        if (WaywardBeyond.GameState < GameState.Playing)
        {
            return;
        }

        //  Lazily resolve the render-coupled builder on the ECS thread (DryIoc provides the Func<T>).
        _resolvedBuilder ??= _voxelBuilder();

        Result<VoxelEditMessage> receiveResult;
        while ((receiveResult = _transport.Receive<VoxelEditMessage>()).Success)
        {
            if (WaywardBeyond.GameState < GameState.Playing)
            {
                return;
            }

            ApplyEdit(receiveResult.Value, store);
        }
    }

    private void ApplyEdit(in VoxelEditMessage message, DataStore store)
    {
        if (!store.TryGet(Uuid.FromValue(message.EntityUuid), out int entity) ||
            !store.TryGet(entity, out VoxelComponent voxelComponent))
        {
            return;
        }

        voxelComponent.VoxelObject.Set(message.X, message.Y, message.Z, message.Voxel);
        store.MarkDirty<VoxelComponent>(entity);

        _resolvedBuilder?.Rebuild(entity);
    }
}