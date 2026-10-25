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

    private const int ROW_HEIGHT = 30;
    private const int ROW_GAP = 3;
    private const int PANEL_PADDING = 12;
    private const int TABS_HEIGHT = 40;
    private const int DIRECT_HEIGHT = 72;
    private const int DIRECT_ERROR_HEIGHT = 96;

    private readonly ClientRpcHost _rpcHost;
    private readonly GameSaveService _gameSaveService;
    private readonly NetworkingConfig _networkingConfig;
    private readonly ProfileSettings _profileSettings;
    private readonly LanDiscoveryService _discovery;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;

    private readonly Widgets.ActionButtonOptions _returnButton;
    private readonly Widgets.ActionButtonOptions _joinButton;

    private TextBoxState _hostTextBox;
    private TextBoxState _portTextBox;
    private bool _favoritesTab = true;
    private int _selectedFavorite;
    private int _selectedLan;
    private int _pendingRemoveFavorite = -1;
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
        _soundEffectService = soundEffectService;
        _localization = localization;

        _returnButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
        _joinButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);

        _hostTextBox = new TextBoxState(
            initialValue: networkingConfig.RemoteHost.Get(),
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.host"),
                MaxCharacters: 253,
                Constraints: new Constraints
                {
                    Width = new Fixed(200),
                    Height = new Fixed(UiMetrics.ControlHeight),
                }
            )
        );

        int defaultPort = networkingConfig.RemotePort.Get();
        _portTextBox = new TextBoxState(
            initialValue: defaultPort.ToString(),
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.port"),
                MaxCharacters: 5,
                DisallowedCharacters: [' ', '\t', '\n', '\r'],
                Constraints: new Constraints
                {
                    Width = new Fixed(70),
                    Height = new Fixed(UiMetrics.ControlHeight),
                }
            )
        );

        _savedServers = [.. profileSettings.SavedServers.Get()];
        _scanning = true;
        Task.Run(ScanServersAsync);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: false
        );

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            ui.Label(
                _localization.GetString("ui.menu.subspaceComms")!,
                layout.Title,
                UiMetrics.MenuTitleFont,
                UiTheme.Accent,
                Anchors.Center
            );

            DrawTabs(ui, layout);

            int directHeight = _errorMessage != null ? DIRECT_ERROR_HEIGHT : DIRECT_HEIGHT;
            IntRect directRect = Widgets.Rect(0, layout.Right.Bottom - directHeight, layout.PageWidth, directHeight);
            int tableTop = layout.Right.Top + TABS_HEIGHT;
            IntRect tableRect = Widgets.Rect(
                0,
                tableTop,
                layout.PageWidth,
                Math.Max(0, directRect.Top - tableTop - 12)
            );

            DrawServerTable(ui, tableRect);
            DrawDirectConnect(ui, directRect, menu);

            if (ui.ActionButton(
                    "Button_Back",
                    _localization.GetString("ui.button.return")!,
                    _returnButton with { Rect = layout.ReturnButton }
                ))
            {
                menu.GoToPage(MenuPage.Home);
            }

            int count = _favoritesTab ? _savedServers.Count : _discoveredServers.Count;
            if (ui.ActionButton(
                    "Button_JoinServer",
                    _localization.GetString("ui.button.joinServer")!,
                    _joinButton with { Rect = layout.PrimaryButton, Enabled = count > 0 }
                ))
            {
                JoinSelected(menu);
            }
        }

        return Result.FromSuccess();
    }

    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int width = Math.Min(180, layout.PageWidth / 2 - 4);
        if (ui.Tab(
                "FavoritesTab",
                Widgets.Rect(0, layout.Right.Top, width, TABS_HEIGHT),
                _localization.GetString("ui.menu.favorites")!,
                _favoritesTab
            ))
        {
            _favoritesTab = true;
            _errorMessage = null;
            _scrollY = 0;
        }

        if (ui.Tab(
                "LanDiscoveryTab",
                Widgets.Rect(width + 4, layout.Right.Top, width, TABS_HEIGHT),
                _localization.GetString("ui.menu.lanDiscovery")!,
                !_favoritesTab
            ))
        {
            _favoritesTab = false;
            _errorMessage = null;
            _scrollY = 0;
        }
    }

    private void DrawServerTable(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetString("ui.menu.serverList")!))
        {
            int contentWidth = rect.Size.X - PANEL_PADDING * 2;
            int deleteWidth = _favoritesTab ? 32 : 0;
            int tableWidth = contentWidth - deleteWidth;
            int nameWidth = Math.Max(110, (int)(tableWidth * 0.30f));
            int addressWidth = Math.Max(130, (int)(tableWidth * 0.38f));
            int playersWidth = Math.Max(50, (int)(tableWidth * 0.12f));
            int pingWidth = Math.Max(80, tableWidth - nameWidth - addressWidth - playersWidth);
            int headerY = 38;
            int listY = 62;
            int listHeight = Math.Max(0, rect.Size.Y - listY - PANEL_PADDING);

            ui.Label(
                _localization.GetString("ui.menu.serverName")!,
                Widgets.Rect(PANEL_PADDING + 32, headerY, nameWidth - 32, 16),
                UiMetrics.MenuLabelFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                _localization.GetString("ui.menu.address")!,
                Widgets.Rect(PANEL_PADDING + nameWidth, headerY, addressWidth, 16),
                UiMetrics.MenuLabelFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                _localization.GetString("ui.menu.players")!,
                Widgets.Rect(PANEL_PADDING + nameWidth + addressWidth, headerY, playersWidth, 16),
                UiMetrics.MenuLabelFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                _localization.GetString("ui.menu.ping")!,
                Widgets.Rect(PANEL_PADDING + nameWidth + addressWidth + playersWidth, headerY, pingWidth, 16),
                UiMetrics.MenuLabelFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Divider(Widgets.Rect(PANEL_PADDING, 56, contentWidth, 1), UiTheme.Border);

            if (!_favoritesTab && ui.ActionButton(
                    "Button_Scan",
                    Widgets.Rect(rect.Size.X - PANEL_PADDING - 80, 6, 80, 22),
                    _localization.GetString("ui.button.scanShort")!,
                    ButtonVariant.Secondary,
                    !_scanning,
                    soundEffectService: _soundEffectService
                ))
            {
                _scanning = true;
                _discoverMessage = null;
                _foundServers.Clear();
                _discoveredServers = [];
                _scrollY = 0;
                _ = ScanServersAsync();
            }

            int count = _favoritesTab ? _savedServers.Count : _discoveredServers.Count;
            if (count == 0)
            {
                string message = _favoritesTab
                    ? _localization.GetString("ui.text.none")!
                    : _scanning
                        ? _localization.GetString("ui.notification.discover.scanning")!
                        : _discoverMessage ?? _localization.GetString("ui.notification.discover.none")!;
                ui.Label(
                    message,
                    Widgets.Rect(PANEL_PADDING, listY + 12, contentWidth, 20),
                    UiMetrics.MenuBodyFont,
                    _favoritesTab ? UiTheme.TextDim : UiTheme.TextMuted,
                    Anchors.Left | Anchors.Center
                );
                return;
            }

            int contentHeight = count * (ROW_HEIGHT + ROW_GAP);
            int maxScroll = Math.Max(0, contentHeight - listHeight);
            float wheel = _inputService.GetMouseScroll();
            if (wheel != 0f)
            {
                _scrollY -= (int)MathF.Round(wheel * UiMetrics.ScrollStep);
            }
            _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Transparent;
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(PANEL_PADDING, listY, contentWidth, listHeight));
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(contentWidth),
                    Height = new Fixed(listHeight),
                };
                ui.ScrollY = -_scrollY;

                for (var i = 0; i < count; i++)
                {
                    int index = i;
                    bool selected = _favoritesTab ? index == _selectedFavorite : index == _selectedLan;
                    int rowY = index * (ROW_HEIGHT + ROW_GAP);
                    IntRect rowRect = Widgets.Rect(0, rowY, contentWidth, ROW_HEIGHT);

                    bool clicked = ui.SelectableCard(
                        _favoritesTab ? $"SavedServer_{index}" : $"Server_{index}",
                        rowRect,
                        new Widgets.SelectableCardOptions(selected),
                        contentUi =>
                        {
                            if (_favoritesTab)
                            {
                                SavedServer server = _savedServers[index];
                                DrawServerRow(
                                    contentUi,
                                    nameWidth,
                                    addressWidth,
                                    playersWidth,
                                    pingWidth,
                                    server.Name,
                                    $"{server.Host}:{server.Port}",
                                    "-",
                                    ping: null
                                );

                                if (contentUi.IconButton(
                                        $"RemoveSavedServer_{index}",
                                        Widgets.Rect(contentWidth - 30, 3, 24, 24),
                                        MenuGlyphs.Trash,
                                        new Widgets.IconButtonOptions(
                                            Danger: true,
                                            SoundEffectService: _soundEffectService
                                        )
                                    ))
                                {
                                    _pendingRemoveFavorite = index;
                                }
                            }
                            else
                            {
                                DiscoveredServer server = _discoveredServers[index];
                                DrawServerRow(
                                    contentUi,
                                    nameWidth,
                                    addressWidth,
                                    playersWidth,
                                    pingWidth,
                                    server.Name,
                                    $"{server.Host}:{server.Port}",
                                    server.Players.ToString(),
                                    ping: null
                                );
                            }
                        }
                    );

                    if (clicked)
                    {
                        if (_favoritesTab)
                        {
                            _selectedFavorite = index;
                        }
                        else
                        {
                            _selectedLan = index;
                        }

                        _errorMessage = null;
                    }
                }
            }

            ui.Scrollbar(
                Widgets.Rect(rect.Size.X - PANEL_PADDING - 6, listY, 6, listHeight),
                contentHeight,
                listHeight,
                _scrollY
            );

            if (_pendingRemoveFavorite >= 0)
            {
                RemoveFavoriteAt(_pendingRemoveFavorite);
                _pendingRemoveFavorite = -1;
            }
        }
    }

    private void DrawServerRow(
        UIBuilder<Material> ui,
        int nameWidth,
        int addressWidth,
        int playersWidth,
        int pingWidth,
        string name,
        string address,
        string players,
        int? ping
    )
    {
        ui.Label(
            _favoritesTab ? MenuGlyphs.Galaxy : MenuGlyphs.Server,
            Widgets.Rect(8, 6, 20, 18),
            UiMetrics.MenuSmallFont,
            UiTheme.Accent,
            Anchors.Center,
            UiTheme.FontIcons
        );
        ui.Label(
            name,
            Widgets.Rect(32, 0, nameWidth - 36, ROW_HEIGHT),
            UiMetrics.MenuBodyFont,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            address,
            Widgets.Rect(nameWidth, 0, addressWidth - 8, ROW_HEIGHT),
            UiMetrics.MenuSmallFont,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            players,
            Widgets.Rect(nameWidth + addressWidth, 0, playersWidth - 8, ROW_HEIGHT),
            UiMetrics.MenuSmallFont,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        ui.PingIndicator(
            Widgets.Rect(nameWidth + addressWidth + playersWidth, 0, pingWidth - 8, ROW_HEIGHT),
            ping
        );
    }

    private void DrawDirectConnect(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(rect))
        {
            const int groupWidth = 496;
            int groupX = (rect.Size.X - groupWidth) / 2;
            int rowY = _errorMessage != null ? 14 : 20;

            ui.Label(
                _localization.GetString("ui.menu.directConnect")!,
                Widgets.Rect(groupX, rowY, 124, UiMetrics.ControlHeight),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            if (ui.IconButton(
                    "Button_SaveServer",
                    Widgets.Rect(groupX + 132, rowY, UiMetrics.ControlHeight, UiMetrics.ControlHeight),
                    MenuGlyphs.Star,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ))
            {
                AddCurrentServer();
            }

            using (ui.Element())
            {
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(groupX + 172, rowY, 156, UiMetrics.ControlHeight));
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(156),
                    Height = new Fixed(UiMetrics.ControlHeight),
                };
                ui.TextBox("TextBox_Host", ref _hostTextBox, new FontOptions { Size = UiMetrics.MenuSmallFont }, _inputService, out Widgets.Interactions hostInteractions);
                if (hostInteractions.Has(Widgets.Interactions.Submit))
                {
                    TryConnect(menu);
                }
            }

            using (ui.Element())
            {
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(groupX + 336, rowY, 60, UiMetrics.ControlHeight));
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(60),
                    Height = new Fixed(UiMetrics.ControlHeight),
                };
                ui.TextBox("TextBox_Port", ref _portTextBox, new FontOptions { Size = UiMetrics.MenuSmallFont }, _inputService, out Widgets.Interactions portInteractions);
                if (portInteractions.Has(Widgets.Interactions.Submit))
                {
                    TryConnect(menu);
                }
            }

            if (ui.ActionButton(
                    "Button_Connect",
                    Widgets.Rect(groupX + 404, rowY, 92, UiMetrics.ControlHeight),
                    _localization.GetString("ui.button.connect")!,
                    ButtonVariant.Primary,
                    soundEffectService: _soundEffectService
                ))
            {
                TryConnect(menu);
            }

            if (_errorMessage != null)
            {
                ui.Label(
                    _errorMessage,
                    Widgets.Rect(groupX, rect.Size.Y - 24, groupWidth, 16),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Danger,
                    Anchors.Left | Anchors.Center
                );
            }
        }
    }

    private int _scrollY;

    private void JoinSelected(Menu<MenuPage> menu)
    {
        if (_favoritesTab)
        {
            if (_selectedFavorite < 0 || _selectedFavorite >= _savedServers.Count)
            {
                return;
            }

            SavedServer server = _savedServers[_selectedFavorite];
            _hostTextBox.Text.Clear().Append(server.Host);
            _portTextBox.Text.Clear().Append(server.Port.ToString());
        }
        else
        {
            if (_selectedLan < 0 || _selectedLan >= _discoveredServers.Count)
            {
                return;
            }

            DiscoveredServer server = _discoveredServers[_selectedLan];
            _hostTextBox.Text.Clear().Append(server.Host);
            _portTextBox.Text.Clear().Append(server.Port.ToString());
        }

        TryConnect(menu);
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

    private void TryConnect(Menu<MenuPage>? menu)
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
        _networkingConfig.RemoteHost.Set(host);
        _networkingConfig.RemotePort.Set(port);
        _networkingConfig.Save();
        _profileSettings.LastServerMode.Set(LastServerMode.Remote);
        _profileSettings.Save();

        _ = _gameSaveService.RefreshLevelsAsync();
        menu?.GoToPage(MenuPage.SelectSave);
    }

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
        _selectedFavorite = 0;
        PersistSavedServers();
        _errorMessage = null;
    }

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

    private void PersistSavedServers()
    {
        _profileSettings.SavedServers.Set([.. _savedServers]);
        _profileSettings.Save();
    }
}
