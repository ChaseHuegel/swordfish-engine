using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MultiplayerPage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Multiplayer;

    private readonly ClientRpcHost _rpcHost;
    private readonly GameSaveService _gameSaveService;
    private readonly NetworkingConfig _networkingConfig;
    private readonly ProfileSettings _profileSettings;
    private readonly LanDiscoveryService _discovery;
    private readonly IInputService _inputService;
    private readonly ILocalization _localization;

    private readonly Widgets.ActionButtonOptions _primaryButton;
    private readonly Widgets.ActionButtonOptions _secondaryButton;
    private readonly Widgets.ActionButtonOptions _subtleButton;
    private readonly Widgets.ActionButtonOptions _serverRowButton;
    private readonly Widgets.ActionButtonOptions _deleteIconButton;
    private readonly FontOptions _bodyFont;

    private TextBoxState _hostTextBox;
    private TextBoxState _portTextBox;
    private string? _errorMessage;
    private bool _scanning;
    private string? _discoverMessage;
    private readonly List<DiscoveredServer> _foundServers = [];
    private IReadOnlyList<DiscoveredServer> _discoveredServers = [];
    private List<SavedServer> _savedServers = [];

    public MultiplayerPage(
        in ClientRpcHost rpcHost,
        in GameSaveService gameSaveService,
        in NetworkingConfig networkingConfig,
        in ProfileSettings profileSettings,
        in LanDiscoveryService discovery,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization
    ) {
        _rpcHost = rpcHost;
        _gameSaveService = gameSaveService;
        _networkingConfig = networkingConfig;
        _profileSettings = profileSettings;
        _discovery = discovery;
        _inputService = inputService;
        _localization = localization;

        _primaryButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _secondaryButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _subtleButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Subtle, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _serverRowButton = Widgets.ActionButtonOptions.ListRow(ButtonVariant.Secondary, soundEffectService) with
        {
            MinWidth = 520,
            Anchors = Anchors.Center,
        };
        _deleteIconButton = Widgets.ActionButtonOptions.Icon(
            ButtonVariant.Danger,
            soundEffectService,
            size: UiMetrics.SmallIconButtonSize,
            fontSize: UiMetrics.IconFont,
            fontId: "Font Awesome 6 Free Solid"
        );
        _bodyFont = new FontOptions
        {
            Size = UiMetrics.BodyFont,
        };

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
                ui.FontSize = UiMetrics.TitleFont;
                ui.Color = UiTheme.Text;
            }
        }

        using (ui.Element())
        {
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints { Anchors = Anchors.Center, };

            //  Enter in either field submits the connect, matching the connect button path.
            ui.TextBox(id: "TextBox_Host", state: ref _hostTextBox, _bodyFont, _inputService, out Widgets.Interactions hostInteractions);
            ui.TextBox(id: "TextBox_Port", state: ref _portTextBox, _bodyFont, _inputService, out Widgets.Interactions portInteractions);
            bool submitted = hostInteractions.Has(Widgets.Interactions.Submit) || portInteractions.Has(Widgets.Interactions.Submit);

            if (_errorMessage != null)
            {
                using (ui.Text(_errorMessage))
                {
                    ui.Color = UiTheme.Danger;
                }
            }

            if (ui.ActionButton("Button_Connect", _localization.GetString("ui.button.connect")!, _primaryButton) || submitted)
            {
                TryConnect(menu);
            }

            //  Saved servers: connect on click, remove via the trash icon.
            if (_savedServers.Count > 0)
            {
                using (ui.Text(_localization.GetString("ui.savedServer.saved")!))
                {
                    ui.FontSize = UiMetrics.ControlFont;
                    ui.Color = UiTheme.TextMuted;
                }

                foreach (SavedServer saved in _savedServers)
                {
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Horizontal;
                        ui.Spacing = 8;
                        ui.Constraints = new Constraints { Anchors = Anchors.Center, };

                        if (ui.ActionButton($"SavedServer_{saved.Host}:{saved.Port}", $"{saved.Name} ({saved.Host}:{saved.Port})", _serverRowButton))
                        {
                            _hostTextBox.Text.Clear().Append(saved.Host);
                            _portTextBox.Text.Clear().Append(saved.Port.ToString());
                            TryConnect(menu);
                        }

                        if (ui.ActionButton($"RemoveSavedServer_{saved.Host}:{saved.Port}", "\uf2ed", _deleteIconButton))
                        {
                            _savedServers.Remove(saved);
                            PersistSavedServers();
                        }
                    }
                }
            }

            if (ui.ActionButton("Button_SaveServer", _localization.GetString("ui.savedServer.add")!, _secondaryButton))
            {
                AddCurrentServer();
            }

            if (_scanning)
            {
                using (ui.Text(_localization.GetString("ui.notification.discover.scanning")!))
                {
                    ui.FontSize = UiMetrics.SmallFont;
                    ui.Color = UiTheme.TextMuted;
                }
            }
            else
            {
                if (ui.ActionButton("Button_Scan", _localization.GetString("ui.button.scan")!, _secondaryButton) && !_scanning)
                {
                    _scanning = true;
                    _discoverMessage = null;
                    _foundServers.Clear();
                    _discoveredServers = [];
                    _ = ScanServersAsync();
                }
            }

            foreach (DiscoveredServer server in _discoveredServers)
            {
                if (ui.ActionButton($"Server_{server.Host}:{server.Port}", $"{server.Name} ({server.Host}:{server.Port}) — {server.Players} players", _serverRowButton))
                {
                    PickServer(in server);
                    TryConnect(menu);
                }
            }

            if (_discoverMessage != null)
            {
                using (ui.Text(_discoverMessage))
                {
                    ui.Color = UiTheme.Danger;
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

            if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.back")!, _subtleButton))
            {
                menu.GoBack();
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

        Result result = _rpcHost.ConnectRemote(host, port);
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
