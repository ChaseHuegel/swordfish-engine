using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Shared.Networking;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Drives the client back to the menu when the active remote <see cref="TcpTransport"/> loses its server.
/// The transport raises <see cref="TransportManager.RemoteDisconnected"/> on its receive thread; this
/// system enqueues the event and does the teardown on the ECS thread, mirroring the join/cleanup
/// threading rules: it saves the character while still in <c>Playing</c>, faults any in-flight world
/// operations, requests the world cleanup, then drops to the menu with a connection-lost toast. It runs
/// for any state (menu, loading, playing) - a player who loses the server mid-join returns to the menu
/// and is told why.
/// </summary>
internal sealed class ClientDisconnectSystem : IEntitySystem
{
    private readonly TransportManager _transport;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly ClientCleanupSystem _cleanupSystem;
    private readonly NotificationService _notificationService;
    private readonly ILocalization _localization;
    private readonly WorldsClient _worlds;

    private readonly ConcurrentQueue<byte> _disconnects = new();

    public ClientDisconnectSystem(
        in TransportManager transport,
        in CharacterSaveManager characterSaveManager,
        in ClientCleanupSystem cleanupSystem,
        in NotificationService notificationService,
        in ILocalization localization,
        in WorldsClient worlds
    ) {
        _transport = transport;
        _characterSaveManager = characterSaveManager;
        _cleanupSystem = cleanupSystem;
        _notificationService = notificationService;
        _localization = localization;
        _worlds = worlds;

        _transport.RemoteDisconnected += () => _disconnects.Enqueue(0);
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
        WaywardBeyond.GameState.Set(GameState.MainMenu);

        //  World-management requests in flight will never be answered by the vanished server: complete
        //  them as failures so the save screen and world UI never await them forever.
        _worlds.FaultPending();

        _notificationService.Push(new Notification(_localization.GetString("notification.connection.lost")!, NotificationType.Toast));
        _transport.Disconnect();
    }

    /// <summary>Enqueues a disconnect (e.g. a join that timed out awaiting the world stream) for the next tick.</summary>
    public void RequestDisconnect()
    {
        _disconnects.Enqueue(0);
    }
}