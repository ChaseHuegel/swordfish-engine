using Krill;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Player;

internal sealed class PlayerData(
    in IAssetDatabase<Item> itemDatabase,
    ISlotGroupSchemaDatabase? schemas = null,
    ILocalizedTagsDatabase? tagDatabase = null
)
{
    private readonly IAssetDatabase<Item> _itemDatabase = itemDatabase;
    private readonly InventoryValidationContext _validationContext = new(schemas, tagDatabase?.Invariant, itemDatabase);

    /// <summary>Shared validation context used by local inventory prediction and UI distribution previews.</summary>
    public InventoryValidationContext ValidationContext => _validationContext;

    public Result<InventoryComponent> GetInventory(DataStore store)
    {
        InventoryComponent value = default;

        store.Query<PlayerComponent, InventoryComponent>(0f, InventoryQuery);
        void InventoryQuery(float delta, DataStore _, int entity, in PlayerComponent player, in InventoryComponent inventory)
        {
            value = inventory;
        }

        return Result<InventoryComponent>.FromSuccess(value);
    }

    public Result<int> GetActiveSlot(DataStore store)
    {
        var slot = 0;

        store.Query<PlayerComponent, EquipmentComponent>(0f, EquipmentQuery);
        void EquipmentQuery(float delta, DataStore _, int entity, in PlayerComponent player, in EquipmentComponent equipment)
        {
            slot = equipment.ActiveInventorySlot;
        }

        return Result<int>.FromSuccess(slot);
    }

    public Result SetActiveSlot(DataStore store, int slot)
    {
        store.QueryRef<PlayerComponent, EquipmentComponent>(0f, EquipmentQuery);
        void EquipmentQuery(float delta, DataStore dataStore, int entity, ref Ref<PlayerComponent> player, ref Ref<EquipmentComponent> equipment)
        {
            int clamped = dataStore.TryGet(entity, out InventoryComponent inventory) && inventory.HasGroup(SlotGroups.HOTBAR)
                ? inventory.ClampHotbarSlot(slot)
                : InventoryComponent.ClampHotbarSlot(slot);

            if (equipment.Read.ActiveInventorySlot != clamped)
            {
                equipment.Write.ActiveInventorySlot = clamped;
            }
        }

        return Result.FromSuccess();
    }

    public Result<ItemSlot> GetMainHand(DataStore store)
    {
        Result<ItemSlot> result = default;

        store.Query<PlayerComponent, InventoryComponent>(delta: 0f, QueryPlayerInventory);
        void QueryPlayerInventory(float delta, DataStore dataStore, int entity, in PlayerComponent player, in InventoryComponent inventory)
        {
            result = GetMainHand(dataStore, entity, inventory);
        }

        return result;
    }

    public Result<ItemSlot> GetMainHand(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out InventoryComponent inventory))
        {
            return Result<ItemSlot>.FromFailure($"No inventory found for player entity: {entity}");
        }

        return GetMainHand(store, entity, inventory);
    }

    public Result<ItemSlot> GetMainHand(DataStore store, int entity, in InventoryComponent inventory)
    {
        if (!store.TryGet(entity, out EquipmentComponent equipment))
        {
            return Result<ItemSlot>.FromFailure($"No equipment found for player entity: {entity}");
        }

        ItemData itemStack = inventory.Get(new SlotId(SlotGroups.HOTBAR, equipment.ActiveInventorySlot));
        if (itemStack.IsEmpty())
        {
            return Result<ItemSlot>.FromFailure(message: null);
        }

        Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);
        if (!itemResult.Success)
        {
            return new Result<ItemSlot>(success: false, value: default, itemResult.Message, itemResult.Exception);
        }

        var itemSlot = new ItemSlot(equipment.ActiveInventorySlot, itemResult);
        return Result<ItemSlot>.FromSuccess(itemSlot);
    }

    /// <summary>
    /// Applies a unified transfer op locally when targeting only the player inventory, then stages
    /// it for upstream replication.
    /// </summary>
    public void ApplyMove(DataStore store, in TransferOp op)
    {
        var predict = new PredictTransferAction(in op, in _validationContext);
        store.QueryRef<PlayerComponent, InventoryComponent, PredictTransferAction>(0f, ref predict);
        StageInventoryOp(store, in op);
    }

    public delegate void InventoryMutation(ref InventoryComponent inventory);

    public void MutateInventory(DataStore store, InventoryMutation mutation)
    {
        store.QueryRef<PlayerComponent, InventoryComponent>(0f, (float _, DataStore _, int _, ref Ref<PlayerComponent> _, ref Ref<InventoryComponent> inventory) =>
        {
            InventoryComponent updated = inventory.Read.Clone();
            mutation(ref updated);
            inventory.Write = updated;
        });
    }

    /// <summary>
    /// Stages a unified inventory transfer op for upstream replication using
    /// <see cref="PendingInventoryComponent"/> sequence numbering.
    /// </summary>
    public void StageInventoryOp(DataStore store, in TransferOp op)
    {
        var stage = new StageTransferAction(in op);
        store.QueryRef<PlayerComponent, PendingInventoryComponent, StageTransferAction>(0f, ref stage);
    }

    /// <summary>
    /// Stages a server-authoritative station command for upstream replication using
    /// <see cref="PendingStationActionComponent"/> sequence numbering with zero client prediction.
    /// </summary>
    public void StageStationAction(DataStore store, in StationActionOp op)
    {
        var stage = new StageStationActionCommand(in op);
        store.Query<PlayerComponent, StageStationActionCommand>(0f, ref stage);
    }

    /// <summary>Reads the local player's server-replicated <see cref="ActiveStationComponent"/>, or <see cref="ActiveStationComponent.None"/>.</summary>
    public ActiveStationComponent GetActiveStation(DataStore store)
    {
        var query = new ActiveStationQuery();
        store.Query<PlayerComponent, ActiveStationQuery>(0f, ref query);
        return query.Station;
    }

    private struct PredictTransferAction(in TransferOp transfer, in InventoryValidationContext validationContext)
        : IForEachRef<PlayerComponent, InventoryComponent>
    {
        private readonly TransferOp _transfer = transfer;
        private readonly InventoryValidationContext _validationContext = validationContext;

        public void Execute(float delta, DataStore store, int entity, ref Ref<PlayerComponent> player, ref Ref<InventoryComponent> inventory)
        {
            ulong selfUuid = store.GetUuid(entity).ToValue();
            bool isSelf = (_transfer.FromContainer == 0UL || _transfer.FromContainer == selfUuid)
                && (_transfer.ToContainer == 0UL || _transfer.ToContainer == selfUuid);
            if (!isSelf)
            {
                return;
            }

            var normalized = new TransferOp(0UL, _transfer.FromSlot, 0UL, _transfer.ToSlot, _transfer.Mode, _transfer.Count);
            InventoryComponent updated = inventory.Read.Clone();
            if (InventoryResolver.Apply(ref updated, in normalized, in _validationContext))
            {
                inventory.Write = updated;
            }
        }
    }

    private struct StageTransferAction(in TransferOp transfer) : IForEachRef<PlayerComponent, PendingInventoryComponent>
    {
        private readonly TransferOp _transfer = transfer;

        public void Execute(float delta, DataStore store, int entity, ref Ref<PlayerComponent> player, ref Ref<PendingInventoryComponent> pending)
        {
            ref PendingInventoryComponent pendingValue = ref pending.Write;
            pendingValue.Outbound.Stage(++pendingValue.NextSequence, in _transfer);
        }
    }

    private struct StageStationActionCommand(in StationActionOp op) : IForEach<PlayerComponent>
    {
        private readonly StationActionOp _op = op;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            PendingStationActionComponent pending = store.TryGet(entity, out PendingStationActionComponent existing)
                && existing.Outbound != null
                ? existing
                : new PendingStationActionComponent();
            pending.Outbound.Stage(++pending.NextSequence, in _op);
            store.AddOrUpdate(entity, pending);
        }
    }

    private struct ActiveStationQuery : IForEach<PlayerComponent>
    {
        public ActiveStationComponent Station;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            Station = store.TryGet(entity, out ActiveStationComponent active)
                ? active
                : ActiveStationComponent.None;
        }
    }
}
