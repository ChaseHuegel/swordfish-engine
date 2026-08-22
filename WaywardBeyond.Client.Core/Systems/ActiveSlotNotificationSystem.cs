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

            ItemStack activeStack = inventory.Contents.Length > Owner._lastActiveSlot ? inventory.Contents[Owner._lastActiveSlot] : ItemStack.Empty;
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