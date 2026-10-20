using Swordfish.ECS;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Receives the server-authoritative skill/notification stream on the ECS thread. A remote
/// <see cref="NotificationMessage"/> is localized entirely client-side (each arg that names a
/// localization key is resolved through the client's own locale files; servers never perform
/// localization) and pushed to the notification service. A <see cref="SkillStateUpdateMessage"/> writes
/// the authoritative per-skill total into the local character save so the autosave stays exact - the
/// client performs no skill math and never predicts XP.
/// </summary>
internal sealed class ClientNotificationSystem : IEntitySystem
{
    private readonly ClientInbound _inbound;
    private readonly NotificationService _notificationService;
    private readonly LocalizedFormatter _localizedFormatter;
    private readonly ILocalization _localization;
    private readonly CharacterSaveManager _characterSaveManager;

    public ClientNotificationSystem(
        in ClientInbound inbound,
        in NotificationService notificationService,
        in LocalizedFormatter localizedFormatter,
        in ILocalization localization,
        in CharacterSaveManager characterSaveManager
    ) {
        _inbound = inbound;
        _notificationService = notificationService;
        _localizedFormatter = localizedFormatter;
        _localization = localization;
        _characterSaveManager = characterSaveManager;
    }

    public void Tick(float delta, DataStore store)
    {
        while (_inbound.Notifications.TryDequeue(out NotificationMessage notification))
        {
            ApplyNotification(notification);
        }

        while (_inbound.SkillUpdates.TryDequeue(out SkillStateUpdateMessage skill))
        {
            ApplySkillState(skill);
        }
    }

    private void ApplySkillState(in SkillStateUpdateMessage update)
    {
        if (string.IsNullOrEmpty(update.SkillId))
        {
            return;
        }

        Character? activeSave = _characterSaveManager.ActiveSave;
        if (activeSave == null)
        {
            return;
        }

        //  Record the server-authoritative total into the client-owned save; the autosave persists it.
        Character character = activeSave.Value;
        character.SetStatistic(update.SkillId, update.TotalXP);
        _characterSaveManager.ActiveSave = character;
    }

    private void ApplyNotification(in NotificationMessage message)
    {
        if (string.IsNullOrEmpty(message.Key))
        {
            return;
        }

        string text = _localizedFormatter.GetString(message.Key, ResolveArgs(message.Args));
        var type = (NotificationType)message.Type;

        //  Bar notifications need their dedupe id and progress; other types never carry them.
        if (type == NotificationType.Bar && message.ID != null && message.Amount != null)
        {
            _notificationService.Push(new Notification(message.ID, text, message.Amount.Value));
            return;
        }

        _notificationService.Push(new Notification(text, type));
    }

    /// <summary>
    /// Resolves each arg that names a localization key through the client's locale files, so servers can
    /// send raw keys for text values. Numbers and unknown strings pass through untouched.
    /// </summary>
    private string[] ResolveArgs(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return [];
        }

        var resolved = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            string arg = args[i] ?? string.Empty;
            resolved[i] = _localization.GetString(arg) ?? arg;
        }

        return resolved;
    }
}