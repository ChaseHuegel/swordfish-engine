using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Items;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Systems;

internal class ActiveSlotNotificationSystem(
    in IAssetDatabase<Item> itemDatabase,
    in NotificationService notificationService
) : EntitySystem<PlayerComponent, EquipmentComponent>
{
    private readonly IAssetDatabase<Item> _itemDatabase = itemDatabase;
    private readonly NotificationService _notificationService = notificationService;
    
    private int _lastActiveSlot;
    
    protected override void OnTick(float delta, DataStore store, int entity, ref PlayerComponent player, ref EquipmentComponent equipment)
    {
        if (equipment.ActiveInventorySlot == _lastActiveSlot)
        {
            //  No change
            return;
        }
        
        _lastActiveSlot = equipment.ActiveInventorySlot;

        //  Attempt to push a notification when the slot changes
        if (!store.TryGet(entity, out InventoryComponent inventory))
        {
            return;
        }

        ItemStack activeStack = inventory.Contents.Length > _lastActiveSlot ? inventory.Contents[_lastActiveSlot] : ItemStack.Empty;
        Result<Item> activeItemResult = _itemDatabase.Get(activeStack.ID);
        if (activeItemResult.Success)
        {
            _notificationService.Push(new Notification(activeItemResult.Value.Name, NotificationType.Action));
        }
    }
}