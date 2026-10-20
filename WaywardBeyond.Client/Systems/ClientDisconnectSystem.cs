using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Networking;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Drives the client back to the menu when the active remote connection loses its server. The RPC
/// host raises <see cref="ClientRpcHost.RemoteDisconnected"/> on the receive pump; this system enqueues
/// the event and does the teardown on the ECS thread: save the character while still in
/// <c>Playing</c>, request world cleanup, then drop to the menu with a connection-lost toast. The RPC
/// host restores the loopback, so the menu can list and load local saves without a restart.
/// </summary>
internal sealed class ClientDisconnectSystem : IEntitySystem
{
    private readonly ClientRpcHost _host;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly ClientCleanupSystem _cleanupSystem;
    private readonly NotificationService _notificationService;
    private readonly ILocalization _localization;

    private readonly ConcurrentQueue<byte> _disconnects = new();

    public ClientDisconnectSystem(
        in ClientRpcHost host,
        in CharacterSaveManager characterSaveManager,
        in ClientCleanupSystem cleanupSystem,
        in NotificationService notificationService,
        in ILocalization localization
    ) {
        _host = host;
        _characterSaveManager = characterSaveManager;
        _cleanupSystem = cleanupSystem;
        _notificationService = notificationService;
        _localization = localization;

        _host.RemoteDisconnected += () => _disconnects.Enqueue(0);
    }

    public void Tick(float delta, DataStore store)
    {
        if (!_disconnects.TryDequeue(out _))
        {
            return;
        }

        //  Save the character before dropping below Playing so the save gate (Playing and above) stays
        //  open long enough to capture inventory/skills. The server is gone, so skip the server flush.
        _characterSaveManager.Save(store);
        _cleanupSystem.RequestCleanup();
        _host.ClearInbound();
        WaywardBeyond.GameState.Set(GameState.MainMenu);

        _notificationService.Push(new Notification(_localization.GetString("notification.connection.lost")!, NotificationType.Toast));
        _host.Disconnect();
    }

    /// <summary>Enqueues a disconnect (e.g. a join that timed out awaiting the world stream) for the next tick.</summary>
    public void RequestDisconnect()
    {
        _disconnects.Enqueue(0);
    }
}
