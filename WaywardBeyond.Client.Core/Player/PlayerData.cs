using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;

namespace WaywardBeyond.Client.Core.Player;

internal sealed class PlayerData(in IAssetDatabase<Item> itemDatabase)
{
    private readonly IAssetDatabase<Item> _itemDatabase = itemDatabase;
    
    public Result<InventoryComponent> GetInventory(DataStore store)
    {
        InventoryComponent value = default;
        
        store.Query<PlayerComponent, InventoryComponent>(0f, InventoryQuery);
        void InventoryQuery(float delta, DataStore _, int entity, ref PlayerComponent player, ref InventoryComponent inventory)
        {
            value = inventory;
        }
        
        return Result<InventoryComponent>.FromSuccess(value);
    }

    public Result<int> GetActiveSlot(DataStore store)
    {
        var slot = 0;
        
        store.Query<PlayerComponent, EquipmentComponent>(0f, EquipmentQuery);
        void EquipmentQuery(float delta, DataStore _, int entity, ref PlayerComponent player, ref EquipmentComponent equipment)
        {
            slot = equipment.ActiveInventorySlot;
        }
        
        return Result<int>.FromSuccess(slot);
    }
    
    public Result SetActiveSlot(DataStore store, int slot)
    {
        store.Query<PlayerComponent, EquipmentComponent>(0f, EquipmentQuery);
        void EquipmentQuery(float delta, DataStore _, int entity, ref PlayerComponent player, ref EquipmentComponent equipment)
        {
            equipment.ActiveInventorySlot = slot;
        }
        
        return Result.FromSuccess();
    }
    
    public Result<ItemSlot> GetMainHand(DataStore store)
    {
        Result<ItemSlot> result = default;
        
        store.Query<PlayerComponent, InventoryComponent>(delta: 0f, QueryPlayerInventory);
        void QueryPlayerInventory(float delta, DataStore store, int entity, ref PlayerComponent player, ref InventoryComponent inventory)
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
        
        ItemStack itemStack = inventory.Contents[equipment.ActiveInventorySlot];
        Result<Item> itemResult = _itemDatabase.Get(itemStack.ID);
        if (!itemResult.Success)
        {
            return new Result<ItemSlot>(success: false, value: default, itemResult.Message, itemResult.Exception);
        }
        
        var itemSlot = new ItemSlot(equipment.ActiveInventorySlot, itemResult);
        return Result<ItemSlot>.FromSuccess(itemSlot);
    }
}