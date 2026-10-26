using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Items;

namespace WaywardBeyond.Client.Systems;

/// <summary>Pushes a localized action notification when the local player changes their active hotbar slot.</summary>
internal sealed class ActiveSlotNotificationSystem(
    in IAssetDatabase<Item> itemDatabase,
    in NotificationService notificationService,
    in ILocalization localization
) : IEntitySystem
{
    private readonly IAssetDatabase<Item> _itemDatabase = itemDatabase;
    private readonly NotificationService _notificationService = notificationService;
    private readonly ILocalization _localization = localization;

    private int _lastActiveSlot;

    private struct ForEachAction : IForEach<PlayerComponent, EquipmentComponent>
    {
        public ActiveSlotNotificationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player, in EquipmentComponent equipment)
        {
            if (equipment.ActiveInventorySlot == Owner._lastActiveSlot)
            {
                return;
            }

            Owner._lastActiveSlot = equipment.ActiveInventorySlot;

            if (!store.TryGet(entity, out InventoryComponent inventory))
            {
                return;
            }

            ItemData activeStack = default;
            if (Owner._lastActiveSlot >= 0 && Owner._lastActiveSlot < inventory.Contents.Length)
            {
                activeStack = inventory.Contents[Owner._lastActiveSlot];
            }

            if (activeStack.Count <= 0 || activeStack.ID == null)
            {
                return;
            }

            Result<Item> activeItemResult = Owner._itemDatabase.Get(activeStack.ID);
            if (activeItemResult.Success)
            {
                string localizedName = Owner._localization.GetString(activeItemResult.Value.Name) ?? activeItemResult.Value.Name;
                Owner._notificationService.Push(new Notification(localizedName, NotificationType.Action));
            }
        }
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = new() { Owner = this };
        store.Query<PlayerComponent, EquipmentComponent, ForEachAction>(delta, ref action);
    }
}
