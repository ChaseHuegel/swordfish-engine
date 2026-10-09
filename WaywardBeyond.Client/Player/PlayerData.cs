using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Client.Player;

internal sealed class PlayerData(in IAssetDatabase<Item> itemDatabase)
{
    private readonly IAssetDatabase<Item> _itemDatabase = itemDatabase;
    
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
        void EquipmentQuery(float delta, DataStore _, int entity, ref Ref<PlayerComponent> player, ref Ref<EquipmentComponent> equipment)
        {
            equipment.Write.ActiveInventorySlot = InventoryComponent.ClampSlot(slot);
        }
        
        return Result.FromSuccess();
    }
    
    public Result<ItemSlot> GetMainHand(DataStore store)
    {
        Result<ItemSlot> result = default;
        
        store.Query<PlayerComponent, InventoryComponent>(delta: 0f, QueryPlayerInventory);
        void QueryPlayerInventory(float delta, DataStore store, int entity, in PlayerComponent player, in InventoryComponent inventory)
        {
            result = GetMainHand(store, entity, inventory);
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
        
        ItemData itemStack = equipment.ActiveInventorySlot >= 0 && equipment.ActiveInventorySlot < inventory.Contents.Length
            ? inventory.Contents[equipment.ActiveInventorySlot]
            : default;
        Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);
        if (!itemResult.Success)
        {
            return new Result<ItemSlot>(success: false, value: default, itemResult.Message, itemResult.Exception);
        }
        
        var itemSlot = new ItemSlot(equipment.ActiveInventorySlot, itemResult);
        return Result<ItemSlot>.FromSuccess(itemSlot);
    }

    /// <summary>
    /// Applies a move op as local prediction via the shared resolver, then stages it for upstream
    /// replication. This is the single supported client entry point for changing a server-owned
    /// inventory: the prediction keeps presentation instant, and the staged op drives the server's
    /// authoritative apply and echo.
    /// </summary>
    public void ApplyMove(DataStore store, in SlotMoveOp op)
    {
        SlotMoveOp move = op;
        MutateInventory(store, (ref InventoryComponent inventory) => SharedInventoryResolver.Apply(ref inventory, move));
        StageInventoryOp(store, move);
    }

    public delegate void InventoryMutation(ref InventoryComponent inventory);

    public void MutateInventory(DataStore store, InventoryMutation mutation)
    {
        store.QueryRef<PlayerComponent, InventoryComponent>(0f, (float delta, DataStore dataStore, int entity, ref Ref<PlayerComponent> player, ref Ref<InventoryComponent> inventory) =>
        {
            mutation(ref inventory.Write);
        });
    }

    /// <summary>
    /// Stages an inventory move op for upstream replication. The op's sequence comes from the player's
    /// <see cref="PendingInventoryComponent"/> so retransmits dedupe server-side.
    /// </summary>
    public void StageInventoryOp(DataStore store, in SlotMoveOp op)
    {
        SlotMoveOp move = op;
        store.QueryRef<PlayerComponent, PendingInventoryComponent>(0f,
            (float _, DataStore dataStore, int _, ref Ref<PlayerComponent> _, ref Ref<PendingInventoryComponent> pending) =>
            {
                ref PendingInventoryComponent pendingValue = ref pending.Write;
                pendingValue.Outbound.Stage(++pendingValue.NextSequence, move);
            });
    }
}