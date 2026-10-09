using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Client.Systems;

internal class ActiveSlotNotificationSystem(
    in IAssetDatabase<Item> itemDatabase,
    in NotificationService notificationService
) : IEntitySystem
{
    private readonly IAssetDatabase<Item> _itemDatabase = itemDatabase;
    private readonly NotificationService _notificationService = notificationService;

    private int _lastActiveSlot;

    private struct ForEachAction : IForEach<PlayerComponent, EquipmentComponent>
    {
        public ActiveSlotNotificationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in EquipmentComponent equipment)
        {
            if (equipment.ActiveInventorySlot == Owner._lastActiveSlot)
            {
                //  No change
                return;
            }

            Owner._lastActiveSlot = equipment.ActiveInventorySlot;

            //  Attempt to push a notification when the slot changes
            if (!store.TryGet(entity, out InventoryComponent inventory))
            {
                return;
            }

            ItemData activeStack = Owner._lastActiveSlot < inventory.Contents.Length ? inventory.Contents[Owner._lastActiveSlot] : default;
            Result<Item> activeItemResult = Owner._itemDatabase.Get(activeStack.ID);
            if (activeItemResult.Success)
            {
                Owner._notificationService.Push(new Notification(activeItemResult.Value.Name, NotificationType.Action));
            }
        }
    }

    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = new() { Owner = this };
        store.Query<PlayerComponent, EquipmentComponent, ForEachAction>(delta, ref action);
    }
}