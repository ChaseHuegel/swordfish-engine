using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MultiplayerPage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Multiplayer;

    private readonly ClientRpcHost _rpcHost;
    private readonly GameSaveService _gameSaveService;
    private readonly GameSaveManager _gameSaveManager;
    private readonly NetworkingConfig _networkingConfig;
    private readonly ProfileSettings _profileSettings;
    private readonly LanDiscoveryService _discovery;
    private readonly ServerPingService _pingService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly AddFavoriteModal _addFavoriteModal;
    private readonly ConnectingModal _connectingModal;
    private readonly ILogger<MultiplayerPage> _logger;
    private readonly ScrollAreaState _scroll = new();
    private readonly MultiplayerView _view;
    private readonly ConcurrentDictionary<string, ServerProbeResult> _probes = new(StringComparer.OrdinalIgnoreCase);

    private Menu<MenuPage>? _menu;
    private bool _favoritesTab = true;
    private int _selectedFavorite;
    private int _selectedLan;
    private string _errorKey = string.Empty;
    private bool _scanning;
    private string _discoverKey = string.Empty;
    private IReadOnlyList<DiscoveredServer> _discoveredServers = [];
    private List<SavedServer> _savedServers = [];
    private long _nextProbeTicks;
    private int _probePassRunning;

    /// <summary>Creates the multiplayer page.</summary>
    public MultiplayerPage(
        in ILogger<MultiplayerPage> logger,
        in ClientRpcHost rpcHost,
        in GameSaveService gameSaveService,
        in GameSaveManager gameSaveManager,
        in NetworkingConfig networkingConfig,
        in ProfileSettings profileSettings,
        in LanDiscoveryService discovery,
        in ServerPingService pingService,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal,
        in AddFavoriteModal addFavoriteModal,
        in ConnectingModal connectingModal
    )
    {
        _logger = logger;
        _rpcHost = rpcHost;
        _gameSaveService = gameSaveService;
        _gameSaveManager = gameSaveManager;
        _networkingConfig = networkingConfig;
        _profileSettings = profileSettings;
        _discovery = discovery;
        _pingService = pingService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;
        _addFavoriteModal = addFavoriteModal;
        _connectingModal = connectingModal;

        _view = new MultiplayerView(localization, _scroll)
        {
            Input = inputService,
            TabRequested = SelectTab,
            ScanRequested = Rescan,
            RowSelected = SelectRow,
            RowActionRequested = RowAction,
            AddFavoriteRequested = AddFavoriteFromDialog,
            DisconnectRequested = DisconnectServer,
            ViewSavesRequested = ViewSaves,
            BackRequested = Back,
            JoinRequested = JoinSelected,
            ConnectRequested = () => TryConnect(),
        };
        _view.HostState.Text.Clear().Append(networkingConfig.RemoteHost.Get());
        _view.PortState.Text.Clear().Append(networkingConfig.RemotePort.Get().ToString());

        _savedServers = [.. profileSettings.SavedServers.Get()];
        _scanning = true;
        Task.Run(ScanServersAsync);
    }

    /// <summary>Draws the multiplayer page.</summary>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _menu = menu;
        MaybeStartProbePass();
        HandleFieldState();

        _scroll.Measure(ui, "Multiplayer.Servers");

        _view.Render(ui, BuildState());
        return Result.FromSuccess();
    }

    /// <summary>Clears the error on typing and starts a connection on submit.</summary>
    private void HandleFieldState()
    {
        if (_view.HostState.Changed || _view.PortState.Changed)
        {
            _errorKey = string.Empty;
        }

        if (_view.HostState.Submitted || _view.PortState.Submitted)
        {
            TryConnect();
        }
    }

    /// <summary>Collects the banner, tabs, rows, and footer state shown this frame.</summary>
    private MultiplayerState BuildState()
    {
        bool remote = _rpcHost.IsRemote;
        int count = _favoritesTab ? _savedServers.Count : _discoveredServers.Count;

        var rows = new List<ServerRowState>(count);
        if (_favoritesTab)
        {
            for (var i = 0; i < _savedServers.Count; i++)
            {
                SavedServer server = _savedServers[i];
                string name = server.Name;
                string players = "-";
                int? pingMs = null;

                if (_probes.TryGetValue(EndpointKey(server.Host, server.Port), out ServerProbeResult probe) && probe.Reachable)
                {
                    if (!string.IsNullOrWhiteSpace(probe.ServerName))
                    {
                        name = probe.ServerName;
                    }

                    players = probe.PlayerCount.ToString();
                    pingMs = probe.PingMs;
                }

                rows.Add(new ServerRowState(
                    name,
                    server.Host,
                    server.Port,
                    players,
                    i == _selectedFavorite,
                    Favorite: false,
                    PingMs: pingMs
                ));
            }
        }
        else
        {
            for (var i = 0; i < _discoveredServers.Count; i++)
            {
                DiscoveredServer server = _discoveredServers[i];
                string name = server.Name;
                string players = server.Players.ToString();
                int? pingMs = null;

                if (_probes.TryGetValue(EndpointKey(server.Host, server.Port), out ServerProbeResult probe))
                {
                    if (probe.Reachable)
                    {
                        if (!string.IsNullOrWhiteSpace(probe.ServerName))
                        {
                            name = probe.ServerName;
                        }

                        players = probe.PlayerCount.ToString();
                        pingMs = probe.PingMs;
                    }
                    else
                    {
                        players = "-";
                    }
                }

                rows.Add(new ServerRowState(
                    name,
                    server.Host,
                    server.Port,
                    players,
                    i == _selectedLan,
                    Favorite: true,
                    PingMs: pingMs
                ));
            }
        }

        return new MultiplayerState(
            remote,
            remote ? ResolveBannerName() : string.Empty,
            _networkingConfig.RemoteHost.Get(),
            _networkingConfig.RemotePort.Get(),
            _favoritesTab,
            _scanning,
            _discoverKey,
            rows,
            _errorKey,
            count > 0
        );
    }

    /// <summary>Gets the saved name of the connected server, or an empty string.</summary>
    private string ResolveBannerName()
    {
        string host = _networkingConfig.RemoteHost.Get();
        int port = _networkingConfig.RemotePort.Get();
        for (var i = 0; i < _savedServers.Count; i++)
        {
            if (_savedServers[i].Host == host && _savedServers[i].Port == port)
            {
                return _savedServers[i].Name;
            }
        }

        return string.Empty;
    }

    private void SelectTab(bool favorites)
    {
        if (_favoritesTab == favorites)
        {
            return;
        }

        _favoritesTab = favorites;
        _errorKey = string.Empty;
        _scroll.Reset();
    }

    private void SelectRow(int index)
    {
        if (_favoritesTab)
        {
            _selectedFavorite = index;
        }
        else
        {
            _selectedLan = index;
        }

        _errorKey = string.Empty;
    }

    private void RowAction(int index)
    {
        if (_favoritesTab)
        {
            if (index < 0 || index >= _savedServers.Count)
            {
                return;
            }

            ConfirmRemoveFavorite(index, _savedServers[index].Name);
            return;
        }

        if (index < 0 || index >= _discoveredServers.Count)
        {
            return;
        }

        DiscoveredServer server = _discoveredServers[index];
        string name = ResolveLiveName(server.Host, server.Port, server.Name);
        AddFavorite(new SavedServer
        {
            Name = string.IsNullOrWhiteSpace(name) ? server.Host : name,
            Host = server.Host,
            Port = server.Port,
        });
    }

    private void Rescan()
    {
        _scanning = true;
        _discoverKey = string.Empty;
        _discoveredServers = [];
        _scroll.Reset();
        _ = ScanServersAsync();
    }

    private void AddFavoriteFromDialog()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _modalMenu.GoToPage(_addFavoriteModal.Create(
            AddFavoriteFromForm,
            _networkingConfig.RemotePort.Get()
        ));
    }

    private void ViewSaves()
    {
        Volatile.Write(ref _nextProbeTicks, 0);
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.SelectSave);
    }

    private void Back()
    {
        Volatile.Write(ref _nextProbeTicks, 0);
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.Home);
    }

    /// <summary>Drops the remote connection and the listing it produced.</summary>
    private void DisconnectServer()
    {
        _rpcHost.Disconnect();
        _gameSaveService.ClearListing();
        _gameSaveManager.ActiveSave = null;
        _errorKey = string.Empty;
        _scroll.Reset();
    }

    /// <summary>Copies the selected server into the connect fields and starts a connection.</summary>
    private void JoinSelected()
    {
        string? serverName;
        if (_favoritesTab)
        {
            if (_selectedFavorite < 0 || _selectedFavorite >= _savedServers.Count)
            {
                return;
            }

            SavedServer server = _savedServers[_selectedFavorite];
            SetFields(server.Host, server.Port);
            serverName = ResolveLiveName(server.Host, server.Port, server.Name);
        }
        else
        {
            if (_selectedLan < 0 || _selectedLan >= _discoveredServers.Count)
            {
                return;
            }

            DiscoveredServer server = _discoveredServers[_selectedLan];
            SetFields(server.Host, server.Port);
            serverName = ResolveLiveName(server.Host, server.Port, server.Name);
        }

        TryConnect(serverName);
    }

    private void SetFields(string host, int port)
    {
        _view.HostState.Text.Clear().Append(host);
        _view.PortState.Text.Clear().Append(port.ToString());
    }

    /// <summary>Collects discovered LAN servers until discovery ends.</summary>
    private async Task ScanServersAsync()
    {
        List<DiscoveredServer> found = [];
        try
        {
            await foreach (DiscoveredServer server in _discovery.ScanAsync())
            {
                found.Add(server);
                _discoveredServers = [.. found];
                _discoverKey = string.Empty;
                _ = Task.Run(() => ProbeSingleAsync(server.Host, server.Port));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LAN discovery ended before the scan completed.");
        }
        finally
        {
            _scanning = false;
            if (_discovery.DiscoveryUnavailable)
            {
                _discoverKey = "ui.message.server.discover.unavailable";
            }
            else if (found.Count == 0)
            {
                _discoverKey = "ui.message.server.discover.empty";
            }
        }
    }

    /// <summary>Validates the connect fields, shows the connecting modal, and connects.</summary>
    private void TryConnect(string? serverName = null)
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);

        string host = _view.HostState.Text.ToString().Trim();
        string portText = _view.PortState.Text.ToString().Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            _errorKey = "ui.message.server.connect.required";
            return;
        }

        if (!int.TryParse(portText, out int port) || port < 1 || port > 65535)
        {
            _errorKey = "ui.message.server.connect.invalidPort";
            return;
        }

        var cts = new CancellationTokenSource();
        string targetText = !string.IsNullOrWhiteSpace(serverName) && serverName != host
            ? $"{serverName} ({host}:{port})"
            : $"{host}:{port}";

        string targetLabel = string.Format(
            _localization.GetRequired("ui.message.server.connecting.target"),
            targetText
        );

        _modalMenu.GoToPage(_connectingModal.Create(
            onCancel: cts.Cancel,
            target: targetLabel
        ));

        _ = Task.Run(() => TryConnectAsync(host, port, cts));
    }

    /// <summary>Connects to the host and closes the connecting modal when it is done.</summary>
    private async Task TryConnectAsync(string host, int port, CancellationTokenSource cts)
    {
        try
        {
            Result result = _rpcHost.ConnectRemote(host, port);
            if (cts.IsCancellationRequested)
            {
                if (result.Success)
                {
                    _rpcHost.Disconnect();
                }

                return;
            }

            if (!result.Success)
            {
                _errorKey = "ui.message.server.connect.failed";
                return;
            }

            _errorKey = string.Empty;
            _networkingConfig.RemoteHost.Set(host);
            _networkingConfig.RemotePort.Set(port);
            _networkingConfig.Save();

            await _gameSaveService.RefreshLevelsAsync();

            if (!cts.IsCancellationRequested)
            {
                _menu?.GoToPage(MenuPage.SelectSave);
            }
        }
        finally
        {
            _connectingModal.Close(_modalMenu);
            cts.Dispose();
        }
    }

    /// <summary>Adds a server from the add-favorite dialog fields.</summary>
    private void AddFavoriteFromForm(string host, int port)
    {
        AddFavorite(new SavedServer { Name = host, Host = host, Port = port });
    }

    /// <summary>Inserts a favorite at the top of the saved list and persists it.</summary>
    private void AddFavorite(SavedServer entry)
    {
        _savedServers.RemoveAll(saved => saved.Host == entry.Host && saved.Port == entry.Port);
        if (_savedServers.Count >= ProfileSettings.MAX_SAVED_SERVERS)
        {
            _savedServers.RemoveAt(_savedServers.Count - 1);
        }

        _savedServers.Insert(0, entry);
        _selectedFavorite = 0;
        if (_favoritesTab)
        {
            _scroll.Reset();
        }

        PersistSavedServers();
        _errorKey = string.Empty;
        _ = Task.Run(() => ProbeSingleAsync(entry.Host, entry.Port));
    }

    /// <summary>Asks for confirmation, then removes the favorite at the index.</summary>
    private void ConfirmRemoveFavorite(int index, string name)
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _modalMenu.GoToPage(_confirmModal.Create(
            () => RemoveFavoriteAt(index),
            _localization.GetRequired("ui.dialog.server.remove"),
            string.Format(
                _localization.GetRequired("ui.dialog.server.remove.body"),
                name
            ),
            "ui.button.server.remove",
            "ui.button.cancel"
        ));
    }

    /// <summary>Removes the favorite at the index and persists the list.</summary>
    private void RemoveFavoriteAt(int index)
    {
        if (index < 0 || index >= _savedServers.Count)
        {
            return;
        }

        _savedServers.RemoveAt(index);
        _selectedFavorite = Math.Clamp(_selectedFavorite, 0, Math.Max(0, _savedServers.Count - 1));
        PersistSavedServers();
    }

    /// <summary>Writes the saved server list to the profile settings.</summary>
    private void PersistSavedServers()
    {
        _profileSettings.SavedServers.Set([.. _savedServers]);
        _profileSettings.Save();
    }

    private void MaybeStartProbePass()
    {
        long now = Environment.TickCount64;
        if (now < Volatile.Read(ref _nextProbeTicks))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _probePassRunning, 1, 0) != 0)
        {
            return;
        }

        List<(string Host, int Port)> targets = CollectProbeTargets();
        int intervalMs = Math.Max(1000, _networkingConfig.Discovery.PingIntervalMs.Get());
        Volatile.Write(ref _nextProbeTicks, now + intervalMs);

        if (targets.Count == 0)
        {
            Interlocked.Exchange(ref _probePassRunning, 0);
            return;
        }

        _ = Task.Run(() => ProbeTargetsAsync(targets));
    }

    private List<(string Host, int Port)> CollectProbeTargets()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<(string Host, int Port)>(_savedServers.Count + _discoveredServers.Count);

        for (var i = 0; i < _savedServers.Count; i++)
        {
            SavedServer server = _savedServers[i];
            if (seen.Add(EndpointKey(server.Host, server.Port)))
            {
                targets.Add((server.Host, server.Port));
            }
        }

        IReadOnlyList<DiscoveredServer> discovered = _discoveredServers;
        for (var i = 0; i < discovered.Count; i++)
        {
            DiscoveredServer server = discovered[i];
            if (seen.Add(EndpointKey(server.Host, server.Port)))
            {
                targets.Add((server.Host, server.Port));
            }
        }

        return targets;
    }

    private async Task ProbeTargetsAsync(List<(string Host, int Port)> targets)
    {
        try
        {
            var tasks = new Task[targets.Count];
            for (var i = 0; i < targets.Count; i++)
            {
                (string host, int port) = targets[i];
                tasks[i] = ProbeSingleAsync(host, port);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Server discovery probe pass ended with an error.");
        }
        finally
        {
            Interlocked.Exchange(ref _probePassRunning, 0);
        }
    }

    private async Task ProbeSingleAsync(string host, int port)
    {
        ServerProbeResult result = await _pingService.ProbeAsync(host, port).ConfigureAwait(false);
        _probes[EndpointKey(host, port)] = result;
    }

    private string ResolveLiveName(string host, int port, string fallback)
    {
        if (_probes.TryGetValue(EndpointKey(host, port), out ServerProbeResult probe)
            && probe.Reachable
            && !string.IsNullOrWhiteSpace(probe.ServerName))
        {
            return probe.ServerName;
        }

        return fallback;
    }

    private static string EndpointKey(string host, int port) => $"{host}:{port}";
}
