using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;

namespace WaywardBeyond.Client.Core.Player;

internal sealed class PlayerData(
    in IAssetDatabase<Item> itemDatabase,
    in IECSContext ecsContext
) {
    private readonly IAssetDatabase<Item> _itemDatabase = itemDatabase;
    private readonly IECSContext _ecsContext = ecsContext;
    
    public Result<ItemSlot> GetMainHand()
    {
        Result<ItemSlot> result = default;
        
        _ecsContext.World.DataStore.Query<PlayerComponent, InventoryComponent>(delta: 0f, QueryPlayerInventory);
        void QueryPlayerInventory(float delta, DataStore store, int entity, ref PlayerComponent player, ref InventoryComponent inventory)
        {
            result = GetMainHand(store, entity, inventory);
        }
        
        return result;
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