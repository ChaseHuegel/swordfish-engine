using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MultiplayerPage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Multiplayer;

    private readonly TransportManager _transportManager;
    private readonly GameSaveService _gameSaveService;
    private readonly NetworkingConfig _networkingConfig;
    private readonly ProfileSettings _profileSettings;
    private readonly LanDiscoveryService _discovery;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;

    private readonly Widgets.ButtonOptions _menuButtonOptions;
    private readonly Widgets.ButtonOptions _buttonOptions;
    private readonly Widgets.ButtonOptions _iconOptions;
    private readonly Widgets.ButtonOptions _smallIconOptions;

    private TextBoxState _hostTextBox;
    private TextBoxState _portTextBox;
    private string? _errorMessage;
    private bool _scanning;
    private string? _discoverMessage;
    private readonly List<DiscoveredServer> _foundServers = [];
    private IReadOnlyList<DiscoveredServer> _discoveredServers = [];
    private List<SavedServer> _savedServers = [];

    public MultiplayerPage(
        in TransportManager transportManager,
        in GameSaveService gameSaveService,
        in NetworkingConfig networkingConfig,
        in ProfileSettings profileSettings,
        in LanDiscoveryService discovery,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization
    ) {
        _transportManager = transportManager;
        _gameSaveService = gameSaveService;
        _networkingConfig = networkingConfig;
        _profileSettings = profileSettings;
        _discovery = discovery;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;

        _menuButtonOptions = new Widgets.ButtonOptions(
            new FontOptions { Size = 32, },
            new Widgets.AudioOptions(soundEffectService)
        );

        _buttonOptions = new Widgets.ButtonOptions(
            new FontOptions { Size = 20, },
            new Widgets.AudioOptions(soundEffectService)
        );

        _iconOptions = new Widgets.ButtonOptions(
            new FontOptions { ID = "Font Awesome 6 Free Solid", Size = 32, },
            new Widgets.AudioOptions(soundEffectService)
        );

        _smallIconOptions = new Widgets.ButtonOptions(
            new FontOptions { ID = "Font Awesome 6 Free Solid", Size = 20, },
            new Widgets.AudioOptions(soundEffectService)
        );

        //  The persisted last-used endpoint prefills the page (the #0028 prefill).
        _hostTextBox = new TextBoxState(
            initialValue: networkingConfig.RemoteHost.Get(),
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.host"),
                MaxCharacters: 253,
                Constraints: new Constraints { Width = new Fixed(300), }
            )
        );

        int defaultPort = networkingConfig.RemotePort.Get();
        _portTextBox = new TextBoxState(
            initialValue: defaultPort.ToString(),
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.port"),
                MaxCharacters: 5,
                DisallowedCharacters: [' ', '\t', '\n', '\r'],
                Constraints: new Constraints { Width = new Fixed(300), }
            )
        );

        _savedServers = [.. profileSettings.SavedServers.Get()];

        _scanning = true;
        Task.Run(ScanServersAsync);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

            using (ui.Text(_localization.GetString("ui.menu.multiplayer")!))
            {
                ui.FontSize = 24;
            }
        }

        using (ui.Element())
        {
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

            //  Enter in either field submits the connect, matching the connect button path.
            ui.TextBox(id: "TextBox_Host", state: ref _hostTextBox, _buttonOptions.FontOptions, _inputService, out Widgets.Interactions hostInteractions);
            ui.TextBox(id: "TextBox_Port", state: ref _portTextBox, _buttonOptions.FontOptions, _inputService, out Widgets.Interactions portInteractions);
            bool submitted = hostInteractions.Has(Widgets.Interactions.Submit) || portInteractions.Has(Widgets.Interactions.Submit);

            if (_errorMessage != null)
            {
                using (ui.Text(_errorMessage))
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
            }

            using (ui.TextButton(id: "Button_Connect", text: _localization.GetString("ui.button.connect")!, _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                if (interactions.Has(Widgets.Interactions.Click) || submitted)
                {
                    TryConnect(menu);
                }
            }

            //  Saved servers: connect on click, remove via the trash icon.
            if (_savedServers.Count > 0)
            {
                using (ui.Text(_localization.GetString("ui.savedServer.saved")!))
                {
                    ui.FontSize = 18;
                }

                foreach (SavedServer saved in _savedServers)
                {
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Horizontal;
                        ui.Spacing = 8;
                        ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                        using (ui.TextButton(id: $"SavedServer_{saved.Host}:{saved.Port}", text: $"{saved.Name} ({saved.Host}:{saved.Port})", _buttonOptions, out Widgets.Interactions serverInteractions))
                        {
                            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                            if (serverInteractions.Has(Widgets.Interactions.Click))
                            {
                                _hostTextBox.Text.Clear().Append(saved.Host);
                                _portTextBox.Text.Clear().Append(saved.Port.ToString());
                                TryConnect(menu);
                            }
                        }

                        using (ui.TextButton(id: $"RemoveSavedServer_{saved.Host}:{saved.Port}", text: "\uf2ed", _smallIconOptions, out Widgets.Interactions removeInteractions))
                        {
                            if (removeInteractions.Has(Widgets.Interactions.Click))
                            {
                                _savedServers.Remove(saved);
                                PersistSavedServers();
                            }
                        }
                    }
                }
            }

            using (ui.TextButton(id: "Button_SaveServer", text: _localization.GetString("ui.savedServer.add")!, _buttonOptions, out Widgets.Interactions saveInteractions))
            {
                ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                if (saveInteractions.Has(Widgets.Interactions.Click))
                {
                    AddCurrentServer();
                }
            }

            if (_scanning)
            {
                using (ui.Text(_localization.GetString("ui.notification.discover.scanning")!))
                {
                    ui.FontSize = 16;
                }
            }
            else
            {
                using (ui.TextButton(id: "Button_Scan", text: _localization.GetString("ui.button.scan")!, _buttonOptions, out Widgets.Interactions scanInteractions))
                {
                    ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                    if (scanInteractions.Has(Widgets.Interactions.Click) && !_scanning)
                    {
                        _scanning = true;
                        _discoverMessage = null;
                        _foundServers.Clear();
                        _discoveredServers = [];
                        _ = ScanServersAsync();
                    }
                }
            }

            foreach (DiscoveredServer server in _discoveredServers)
            {
                using (ui.TextButton(id: $"Server_{server.Host}:{server.Port}", text: $"{server.Name} ({server.Host}:{server.Port}) — {server.Players} players", _buttonOptions, out Widgets.Interactions serverInteractions))
                {
                    ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                    if (serverInteractions.Has(Widgets.Interactions.Click))
                    {
                        PickServer(in server);
                        TryConnect(menu);
                    }
                }
            }

            if (_discoverMessage != null)
            {
                using (ui.Text(_discoverMessage))
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
            }
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints { Width = new Fill(), Height = new Fill(), };
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

            using (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                if (interactions.Has(Widgets.Interactions.Click))
                {
                    menu.GoBack();
                }
            }
        }

        return Result.FromSuccess();
    }

    private async Task ScanServersAsync()
    {
        try
        {
            await foreach (DiscoveredServer server in _discovery.ScanAsync())
            {
                _foundServers.Add(server);
                _discoveredServers = _foundServers.ToArray();
                _discoverMessage = null;
            }
        }
        catch (Exception)
        {
            //  Discovery ended badly; keep whatever was already found.
        }
        finally
        {
            _scanning = false;
            if (_discovery.DiscoveryUnavailable)
            {
                _discoverMessage = _localization.GetString("ui.notification.discover.unavailable");
            }
            else if (_foundServers.Count == 0)
            {
                _discoverMessage = _localization.GetString("ui.notification.discover.none");
            }
        }
    }

    private void PickServer(in DiscoveredServer server)
    {
        _hostTextBox.Text.Clear().Append(server.Host);
        _portTextBox.Text.Clear().Append(server.Port.ToString());
        _discoverMessage = null;
    }

    private void TryConnect(Menu<MenuPage> menu)
    {
        string host = _hostTextBox.Text.ToString().Trim();
        string portText = _portTextBox.Text.ToString().Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            _errorMessage = _localization.GetString("ui.notification.connect.hostRequired");
            return;
        }

        if (!int.TryParse(portText, out int port) || port < 1 || port > 65535)
        {
            _errorMessage = _localization.GetString("ui.notification.connect.portInvalid");
            return;
        }

        Result result = _transportManager.ConnectRemote(host, port);
        if (!result.Success)
        {
            _errorMessage = _localization.GetString("ui.notification.connect.failed");
            return;
        }

        _errorMessage = null;

        //  The connect attempt becomes the persisted last-used endpoint (and the remote continue marker).
        _networkingConfig.RemoteHost.Set(host);
        _networkingConfig.RemotePort.Set(port);
        _networkingConfig.Save();
        _profileSettings.LastServerMode.Set(LastServerMode.Remote);
        _profileSettings.Save();

        _ = _gameSaveService.RefreshLevelsAsync();
        menu.GoToPage(MenuPage.SelectSave);
    }

    /// <summary>Saves the page's current endpoint as a named entry, deduped by host:port and capped at 32.</summary>
    private void AddCurrentServer()
    {
        string host = _hostTextBox.Text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(_portTextBox.Text.ToString().Trim(), out int port) || port < 1 || port > 65535)
        {
            _errorMessage = _localization.GetString("ui.notification.connect.portInvalid");
            return;
        }

        SavedServer entry = new() { Name = host, Host = host, Port = port };
        _savedServers.RemoveAll(saved => saved.Host == host && saved.Port == port);
        if (_savedServers.Count >= ProfileSettings.MaxSavedServers)
        {
            _savedServers.RemoveAt(_savedServers.Count - 1);
        }

        _savedServers.Insert(0, entry);
        PersistSavedServers();
        _errorMessage = null;
    }

    private void PersistSavedServers()
    {
        _profileSettings.SavedServers.Set([.. _savedServers]);
        _profileSettings.Save();
    }
}