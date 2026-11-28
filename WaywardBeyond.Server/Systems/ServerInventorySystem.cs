using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Permissions;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-side consumption of inbound inventory operations. Runs between the replication ApplyStage and
/// interaction processing, validating slot filters, stack limits, container reach, and viewer/lock access
/// before atomically applying updates and marking mutated inventories dirty for replication.
/// </summary>
public sealed class ServerInventorySystem(
    ISlotGroupSchemaDatabase? schemas = null,
    ILocalizedTagsDatabase? tagDatabase = null,
    IItemDatabase? itemDatabase = null,
    SessionManager? sessions = null,
    IUserPermissionService? permissions = null
) : IServerWorldSystem
{
    private readonly SessionManager? _sessions = sessions;
    private readonly IUserPermissionService? _permissions = permissions;
    private readonly InventoryValidationContext _context = new(schemas, tagDatabase?.Invariant, itemDatabase);

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        ConsumeOpsAction action = new() { Owner = this };
        store.QueryRef<NetworkComponent, InventoryComponent, ConsumeOpsAction>(delta, ref action);
    }

    private bool Consume(
        DataStore store,
        int entity,
        in NetworkComponent net,
        in InventoryComponent inventory,
        out InventoryComponent updated,
        out uint consumedSequence
    )
    {
        consumedSequence = net.LastConsumedInventorySequence;
        updated = default;

        StageRing<InventoryOp>? staged = net.StagedInventoryOps;
        if (staged == null || staged.Count == 0)
        {
            return false;
        }

        Result<InventoryOp> stagedOp = staged.Consume(consumedSequence);
        if (!stagedOp.Success)
        {
            staged.Clear();
            return false;
        }

        Uuid playerUuid = store.GetUuid(entity);
        updated = inventory.Clone();
        var playerChanged = false;

        while (stagedOp.Success)
        {
            InventoryOp op = stagedOp.Value;
            consumedSequence = op.SequenceNumber;

            //  An invalid op is a no-op, but its sequence is still consumed so it cannot replay.
            ApplyTransferOp(store, entity, playerUuid, ref updated, in op.Transfer, ref playerChanged);
            stagedOp = staged.Consume(consumedSequence);
        }

        staged.Clear();
        return playerChanged;
    }

    private void ApplyTransferOp(
        DataStore store,
        int playerEntity,
        Uuid playerUuid,
        ref InventoryComponent playerInventory,
        in TransferOp transfer,
        ref bool playerChanged
    )
    {
        if (playerUuid == Uuid.Null)
        {
            return;
        }

        Uuid fromUuid = transfer.FromContainer == 0UL ? playerUuid : Uuid.FromValue(transfer.FromContainer);
        Uuid toUuid = transfer.ToContainer == 0UL ? playerUuid : Uuid.FromValue(transfer.ToContainer);
        var normalized = new TransferOp(fromUuid.ToValue(), transfer.FromSlot, toUuid.ToValue(), transfer.ToSlot, transfer.Mode, transfer.Count);

        if (fromUuid == playerUuid && toUuid == playerUuid)
        {
            playerChanged |= InventoryResolver.Apply(ref playerInventory, in normalized, in _context);
            return;
        }

        if (fromUuid == toUuid)
        {
            if (!TryAuthorizeRemoteContainer(store, playerEntity, playerUuid, fromUuid, out int remoteEntity, out InventoryComponent remoteInventory))
            {
                return;
            }

            InventoryComponent remoteCopy = remoteInventory.Clone();
            if (InventoryResolver.Apply(ref remoteCopy, in normalized, in _context))
            {
                store.AddOrUpdate(remoteEntity, remoteCopy);
            }

            return;
        }

        bool fromIsPlayer = fromUuid == playerUuid;
        bool toIsPlayer = toUuid == playerUuid;
        int fromEntity = playerEntity;
        int toEntity = playerEntity;
        InventoryComponent fromRemote = default;
        InventoryComponent toRemote = default;

        if (!fromIsPlayer && !TryAuthorizeRemoteContainer(store, playerEntity, playerUuid, fromUuid, out fromEntity, out fromRemote))
        {
            return;
        }

        if (!toIsPlayer && !TryAuthorizeRemoteContainer(store, playerEntity, playerUuid, toUuid, out toEntity, out toRemote))
        {
            return;
        }

        if (fromIsPlayer)
        {
            InventoryComponent toCopy = toRemote.Clone();
            if (InventoryResolver.Apply(ref playerInventory, ref toCopy, in normalized, in _context))
            {
                playerChanged = true;
                store.AddOrUpdate(toEntity, toCopy);
            }

            return;
        }

        if (toIsPlayer)
        {
            InventoryComponent fromCopy = fromRemote.Clone();
            if (InventoryResolver.Apply(ref fromCopy, ref playerInventory, in normalized, in _context))
            {
                playerChanged = true;
                store.AddOrUpdate(fromEntity, fromCopy);
            }

            return;
        }

        if (fromEntity != toEntity)
        {
            InventoryComponent fromCopy = fromRemote.Clone();
            InventoryComponent toCopy = toRemote.Clone();
            if (InventoryResolver.Apply(ref fromCopy, ref toCopy, in normalized, in _context))
            {
                store.AddOrUpdate(fromEntity, fromCopy);
                store.AddOrUpdate(toEntity, toCopy);
            }
        }
    }

    private bool TryAuthorizeRemoteContainer(
        DataStore store,
        int playerEntity,
        Uuid playerUuid,
        Uuid containerUuid,
        out int containerEntity,
        out InventoryComponent containerInventory
    )
    {
        containerInventory = default;
        if (containerUuid == Uuid.Null
            || !store.TryGet(containerUuid, out containerEntity)
            || containerEntity == playerEntity)
        {
            containerEntity = default;
            return false;
        }

        if (store.Has(typeof(OwnedCharacterComponent), containerEntity)
            || (_sessions != null && _sessions.TryGetClient(containerEntity, out _)))
        {
            return false;
        }

        if (!store.TryGet(containerEntity, out containerInventory))
        {
            return false;
        }

        if (!store.TryGet(playerEntity, out TransformComponent playerTransform)
            || !IsFinite(playerTransform.Position)
            || !TryResolveContainerPosition(store, containerEntity, out Vector3 containerPosition))
        {
            return false;
        }

        float maxReach = CharacterDefaults.DEFAULT_REACH;
        if (store.TryGet(containerEntity, out ContainerAccessComponent access))
        {
            if (access.MaxReach > 0f && float.IsFinite(access.MaxReach))
            {
                maxReach = access.MaxReach;
            }

            if (access.ActiveViewer != Uuid.Null && access.ActiveViewer != playerUuid)
            {
                return false;
            }

            if (access.IsLocked && access.Owner != playerUuid)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(access.RequiredPermission) && !HasContainerPermission(playerEntity, access.RequiredPermission))
            {
                return false;
            }
        }

        return Vector3.DistanceSquared(playerTransform.Position, containerPosition) <= maxReach * maxReach;
    }

    private static bool TryResolveContainerPosition(DataStore store, int containerEntity, out Vector3 position)
    {
        if (store.TryGet(containerEntity, out TransformComponent containerTransform)
            && IsFinite(containerTransform.Position))
        {
            position = containerTransform.Position;
            return true;
        }

        if (store.TryGet(containerEntity, out BrickAnchorComponent anchor)
            && ServerBlockEntityService.TryResolveWorldPosition(store, in anchor, out position))
        {
            return true;
        }

        position = default;
        return false;
    }

    private bool HasContainerPermission(int playerEntity, string permission)
    {
        if (_permissions == null || _sessions == null || !_sessions.TryGetClient(playerEntity, out Uuid clientId))
        {
            return false;
        }

        return _permissions.HasPermission(clientId, permission);
    }

    private static bool IsFinite(Vector3 position)
    {
        return float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);
    }

    private struct ConsumeOpsAction : IForEachRef<NetworkComponent, InventoryComponent>
    {
        public ServerInventorySystem Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<NetworkComponent> net, ref Ref<InventoryComponent> inventory)
        {
            ref readonly NetworkComponent current = ref net.Read;
            ref readonly InventoryComponent inventoryValue = ref inventory.Read;

            if (Owner.Consume(store, entity, in current, in inventoryValue, out InventoryComponent updated, out uint consumedSequence))
            {
                //  Change-gated: an unchanged inventory would still be marked dirty and re-published.
                inventory.Write = updated;
            }

            //  Change-gated: a write of an unchanged value still marks the component dirty.
            if (consumedSequence != current.LastConsumedInventorySequence)
            {
                net.Write.LastConsumedInventorySequence = consumedSequence;
            }
        }
    }
}
