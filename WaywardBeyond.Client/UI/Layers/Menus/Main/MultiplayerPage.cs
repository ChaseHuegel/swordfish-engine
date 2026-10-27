using Microsoft.Extensions.Logging;
using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
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
    private const int BANNER_HEIGHT = 40;
    private const int BANNER_GAP = 8;
    private const int DIRECT_HEIGHT = 60;
    private const int ADD_BUTTON_HEIGHT = 32;

    private readonly ClientRpcHost _rpcHost;
    private readonly GameSaveService _gameSaveService;
    private readonly GameSaveManager _gameSaveManager;
    private readonly NetworkingConfig _networkingConfig;
    private readonly ProfileSettings _profileSettings;
    private readonly LanDiscoveryService _discovery;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly AddFavoriteModal _addFavoriteModal;
    private readonly ConnectingModal _connectingModal;
    private readonly ILogger<MultiplayerPage> _logger;

    private readonly Widgets.ActionButtonOptions _returnButton;
    private readonly Widgets.ActionButtonOptions _joinButton;
    private readonly Widgets.ActionButtonOptions _viewSavesButton;
    private readonly Widgets.ActionButtonOptions _disconnectButton;

    private TextBoxState _hostTextBox;
    private TextBoxState _portTextBox;
    private bool _favoritesTab = true;
    private int _selectedFavorite;
    private int _selectedLan;
    private int _scrollY;
    private string? _errorMessage;
    private bool _scanning;
    private string? _discoverMessage;
    private IReadOnlyList<DiscoveredServer> _discoveredServers = [];
    private List<SavedServer> _savedServers = [];

    /// <summary>Creates the multiplayer page.</summary>
    public MultiplayerPage(
        in ILogger<MultiplayerPage> logger,
        in ClientRpcHost rpcHost,
        in GameSaveService gameSaveService,
        in GameSaveManager gameSaveManager,
        in NetworkingConfig networkingConfig,
        in ProfileSettings profileSettings,
        in LanDiscoveryService discovery,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal,
        in AddFavoriteModal addFavoriteModal,
        in ConnectingModal connectingModal
    ) {
        _logger = logger;
        _rpcHost = rpcHost;
        _gameSaveService = gameSaveService;
        _gameSaveManager = gameSaveManager;
        _networkingConfig = networkingConfig;
        _profileSettings = profileSettings;
        _discovery = discovery;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;
        _addFavoriteModal = addFavoriteModal;
        _connectingModal = connectingModal;

        _returnButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
        _joinButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);
        _viewSavesButton = Widgets.ActionButtonOptions.Compact(ButtonVariant.Primary, soundEffectService);
        _disconnectButton = Widgets.ActionButtonOptions.Compact(ButtonVariant.Secondary, soundEffectService);

        _hostTextBox = new TextBoxState(
            initialValue: networkingConfig.RemoteHost.Get(),
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.host"),
                MaxCharacters: 253,
                Constraints: new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
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
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                }
            )
        );

        _savedServers = [.. profileSettings.SavedServers.Get()];
        _scanning = true;
        Task.Run(ScanServersAsync);
    }

    /// <summary>Draws the multiplayer page.</summary>
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

            int tabsTop = layout.Right.Top;
            if (_rpcHost.IsRemote)
            {
                DrawConnectionBanner(ui, layout, menu, tabsTop);
                tabsTop += BANNER_HEIGHT + BANNER_GAP;
            }

            DrawTabs(ui, layout, tabsTop);

            IntRect directRect = Widgets.Rect(
                0,
                layout.Right.Bottom - DIRECT_HEIGHT,
                layout.PageWidth,
                DIRECT_HEIGHT
            );
            int tableTop = tabsTop + TABS_HEIGHT;
            IntRect tableRect = Widgets.Rect(
                0,
                tableTop,
                layout.PageWidth,
                Math.Max(0, directRect.Top - tableTop - 12)
            );

            DrawServerTable(ui, tableRect);
            DrawDirectConnect(ui, directRect, menu);

            int count = _favoritesTab ? _savedServers.Count : _discoveredServers.Count;
            var back = new MenuLayout.MenuFooterButton(
                "Button_Back",
                _localization.GetRequired("ui.button.return"),
                _returnButton
            );
            var primary = new MenuLayout.MenuFooterButton(
                "Button_JoinServer",
                _localization.GetRequired("ui.button.joinServer"),
                _joinButton with { Enabled = count > 0 }
            );

            MenuLayout.MenuFooterResult footer = layout.DrawFooter(ui, _errorMessage, back, primary);
            if (footer.BackClicked)
            {
                menu.GoToPage(MenuPage.Home);
            }

            if (footer.PrimaryClicked)
            {
                JoinSelected(menu);
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the favorites and LAN discovery tabs.</summary>
    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout, int top)
    {
        int width = Math.Min(180, layout.PageWidth / 2 - 4);

        IntRect favoritesRect = Widgets.Rect(0, top, width, TABS_HEIGHT);
        bool favoritesClicked = ui.Tab(
            "FavoritesTab",
            favoritesRect,
            _localization.GetRequired("ui.menu.favorites"),
            _favoritesTab
        );
        if (favoritesClicked)
        {
            _favoritesTab = true;
            _errorMessage = null;
            _scrollY = 0;
        }

        IntRect lanRect = Widgets.Rect(width + 4, top, width, TABS_HEIGHT);
        bool lanClicked = ui.Tab(
            "LanDiscoveryTab",
            lanRect,
            _localization.GetRequired("ui.menu.lanDiscovery"),
            !_favoritesTab
        );
        if (lanClicked)
        {
            _favoritesTab = false;
            _errorMessage = null;
            _scrollY = 0;
        }
    }

    /// <summary>Draws the banner that names the connected server.</summary>
    private void DrawConnectionBanner(
        UIBuilder<Material> ui,
        in MenuLayout layout,
        Menu<MenuPage> menu,
        int top
    )
    {
        int width = layout.PageWidth;
        using (ui.Element())
        {
            ui.Color = UiTheme.PanelBackground;
            ui.Constraints = Widgets.RectConstraints(Widgets.Rect(0, top, width, BANNER_HEIGHT));
        }
        ui.Divider(
            Widgets.Rect(0, top + BANNER_HEIGHT - 1, width, 1),
            UiTheme.BorderInner
        );

        string host = _networkingConfig.RemoteHost.Get();
        int port = _networkingConfig.RemotePort.Get();
        string? savedName = null;
        for (var i = 0; i < _savedServers.Count; i++)
        {
            if (_savedServers[i].Host == host && _savedServers[i].Port == port)
            {
                savedName = _savedServers[i].Name;
                break;
            }
        }

        string server = !string.IsNullOrWhiteSpace(savedName) ? savedName : $"{host}:{port}";
        const int disconnectWidth = 120;
        const int viewWidth = 120;
        const int gap = 8;
        int viewX = width - PANEL_PADDING - viewWidth;
        int disconnectX = viewX - gap - disconnectWidth;

        ui.Label(
            MenuGlyphs.SERVER,
            Widgets.Rect(PANEL_PADDING, top, 16, BANNER_HEIGHT),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Accent,
            Anchors.Center,
            UiTheme.FONT_ICONS
        );
        ui.Label(
            $"{_localization.GetRequired("ui.menu.connectedTo")} {server}",
            Widgets.Rect(
                PANEL_PADDING + 20,
                top,
                Math.Max(0, disconnectX - PANEL_PADDING - 32),
                BANNER_HEIGHT
            ),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );

        Widgets.ActionButtonOptions disconnectOptions = _disconnectButton with
        {
            Rect = Widgets.Rect(
                disconnectX,
                top + 4,
                disconnectWidth,
                UiMetrics.COMPACT_BUTTON_HEIGHT
            ),
        };
        if (ui.ActionButton(
                "Button_Disconnect",
                _localization.GetRequired("ui.button.disconnect"),
                disconnectOptions
            ))
        {
            DisconnectServer();
        }

        Widgets.ActionButtonOptions viewSavesOptions = _viewSavesButton with
        {
            Rect = Widgets.Rect(
                viewX,
                top + 4,
                viewWidth,
                UiMetrics.COMPACT_BUTTON_HEIGHT
            ),
        };
        if (ui.ActionButton(
                "Button_ViewSaves",
                _localization.GetRequired("ui.button.viewSaves"),
                viewSavesOptions
            ))
        {
            menu.GoToPage(MenuPage.SelectSave);
        }
    }

    /// <summary>Drops the remote connection and the listing it produced.</summary>
    private void DisconnectServer()
    {
        _rpcHost.Disconnect();
        _gameSaveService.ClearListing();
        _gameSaveManager.ActiveSave = null;
        _errorMessage = null;
        _scrollY = 0;
    }

    /// <summary>Draws the server list panel.</summary>
    private void DrawServerTable(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.serverList")))
        {
            int contentWidth = rect.Size.X - PANEL_PADDING * 2;
            const int actionWidth = 32;
            int tableWidth = contentWidth - actionWidth;
            int nameWidth = Math.Max(110, (int)(tableWidth * 0.30f));
            int addressWidth = Math.Max(130, (int)(tableWidth * 0.38f));
            int playersWidth = Math.Max(50, (int)(tableWidth * 0.12f));
            int pingWidth = Math.Max(80, tableWidth - nameWidth - addressWidth - playersWidth);
            const int listY = 62;
            int listBottom = rect.Size.Y - PANEL_PADDING - (_favoritesTab ? ADD_BUTTON_HEIGHT + 8 : 0);
            int listHeight = Math.Max(0, listBottom - listY);

            DrawServerTableHeader(ui, rect, nameWidth, addressWidth, playersWidth, pingWidth);
            DrawScanButton(ui, rect);

            int count = _favoritesTab ? _savedServers.Count : _discoveredServers.Count;
            if (count == 0)
            {
                DrawServerTableEmptyState(ui, contentWidth, listY);
            }
            else
            {
                IntRect listRect = Widgets.Rect(PANEL_PADDING, listY, contentWidth, listHeight);
                IntRect scrollbarRect = Widgets.Rect(
                    rect.Size.X - PANEL_PADDING - 6,
                    listY,
                    6,
                    listHeight
                );
                int contentHeight = count * (ROW_HEIGHT + ROW_GAP);
                ui.ScrollArea(
                    listRect,
                    contentHeight,
                    ref _scrollY,
                    _inputService,
                    scrollbarRect,
                    body => DrawServerRows(
                        body,
                        contentWidth,
                        nameWidth,
                        addressWidth,
                        playersWidth,
                        pingWidth,
                        count
                    )
                );
            }

            DrawAddFavoriteButton(ui, rect, contentWidth);
        }
    }

    /// <summary>Draws the server table column headers and the divider under them.</summary>
    private void DrawServerTableHeader(
        UIBuilder<Material> ui,
        IntRect rect,
        int nameWidth,
        int addressWidth,
        int playersWidth,
        int pingWidth
    )
    {
        int contentWidth = rect.Size.X - PANEL_PADDING * 2;
        const int headerY = 38;

        ui.Label(
            _localization.GetRequired("ui.menu.serverName"),
            Widgets.Rect(PANEL_PADDING + 32, headerY, nameWidth - 32, 16),
            UiMetrics.MENU_LABEL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            _localization.GetRequired("ui.menu.address"),
            Widgets.Rect(PANEL_PADDING + nameWidth, headerY, addressWidth, 16),
            UiMetrics.MENU_LABEL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            _localization.GetRequired("ui.menu.players"),
            Widgets.Rect(PANEL_PADDING + nameWidth + addressWidth, headerY, playersWidth, 16),
            UiMetrics.MENU_LABEL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            _localization.GetRequired("ui.menu.ping"),
            Widgets.Rect(PANEL_PADDING + nameWidth + addressWidth + playersWidth, headerY, pingWidth, 16),
            UiMetrics.MENU_LABEL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Divider(Widgets.Rect(PANEL_PADDING, 56, contentWidth, 1), UiTheme.Border);
    }

    /// <summary>Draws the LAN scan button on the discovery tab and restarts the scan.</summary>
    private void DrawScanButton(UIBuilder<Material> ui, IntRect rect)
    {
        if (_favoritesTab)
        {
            return;
        }

        IntRect scanRect = Widgets.Rect(rect.Size.X - PANEL_PADDING - 80, 6, 80, 22);
        bool clicked = ui.ActionButton(
            "Button_Scan",
            scanRect,
            _localization.GetRequired("ui.button.scanShort"),
            ButtonVariant.Secondary,
            !_scanning,
            soundEffectService: _soundEffectService
        );
        if (!clicked)
        {
            return;
        }

        _scanning = true;
        _discoverMessage = null;
        _discoveredServers = [];
        _scrollY = 0;
        _ = ScanServersAsync();
    }

    /// <summary>Draws the label shown when the active tab has no servers.</summary>
    private void DrawServerTableEmptyState(UIBuilder<Material> ui, int contentWidth, int listY)
    {
        string message = _favoritesTab
            ? _localization.GetRequired("ui.text.none")
            : _scanning
                ? _localization.GetRequired("ui.notification.discover.scanning")
                : _discoverMessage ?? _localization.GetRequired("ui.notification.discover.none");
        ui.Label(
            message,
            Widgets.Rect(PANEL_PADDING, listY + 12, contentWidth, 20),
            UiMetrics.MENU_BODY_FONT,
            _favoritesTab ? UiTheme.TextDim : UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
    }

    /// <summary>Draws every server row inside the scroll viewport.</summary>
    private void DrawServerRows(
        UIBuilder<Material> ui,
        int contentWidth,
        int nameWidth,
        int addressWidth,
        int playersWidth,
        int pingWidth,
        int count
    )
    {
        for (var index = 0; index < count; index++)
        {
            bool selected = _favoritesTab ? index == _selectedFavorite : index == _selectedLan;
            int rowY = index * (ROW_HEIGHT + ROW_GAP);
            IntRect rowRect = Widgets.Rect(0, rowY, contentWidth, ROW_HEIGHT);
            var options = new Widgets.SelectableCardOptions(selected);
            var context = new ServerRowContext(
                this,
                _favoritesTab,
                index,
                nameWidth,
                addressWidth,
                playersWidth,
                pingWidth,
                contentWidth,
                _soundEffectService
            );

            bool clicked = ui.SelectableCard(
                _favoritesTab ? $"SavedServer_{index}" : $"Server_{index}",
                rowRect,
                in options,
                context,
                static (contentUi, row) => DrawServerRowContent(contentUi, in row)
            );
            if (!clicked)
            {
                continue;
            }

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

    /// <summary>Draws one server row and its row action button.</summary>
    private static void DrawServerRowContent(UIBuilder<Material> contentUi, in ServerRowContext context)
    {
        if (context.Favorites)
        {
            DrawSavedServerRowContent(contentUi, in context);
            return;
        }

        DrawDiscoveredServerRowContent(contentUi, in context);
    }

    /// <summary>Draws one saved-server row with its remove button.</summary>
    private static void DrawSavedServerRowContent(UIBuilder<Material> contentUi, in ServerRowContext context)
    {
        MultiplayerPage page = context.Page;
        SavedServer server = page._savedServers[context.Index];
        page.DrawServerRow(
            contentUi,
            context.NameWidth,
            context.AddressWidth,
            context.PlayersWidth,
            context.PingWidth,
            server.Name,
            $"{server.Host}:{server.Port}",
            "-",
            ping: null
        );

        IntRect actionRect = Widgets.Rect(context.ContentWidth - 30, 3, 24, 24);
        var options = new Widgets.IconButtonOptions(
            Danger: true,
            SoundEffectService: context.SoundEffectService
        );
        if (contentUi.IconButton($"RemoveSavedServer_{context.Index}", actionRect, MenuGlyphs.TRASH, options))
        {
            page.ConfirmRemoveFavorite(context.Index, server.Name);
        }
    }

    /// <summary>Draws one discovered-server row with its favorite button.</summary>
    private static void DrawDiscoveredServerRowContent(UIBuilder<Material> contentUi, in ServerRowContext context)
    {
        MultiplayerPage page = context.Page;
        DiscoveredServer server = page._discoveredServers[context.Index];
        page.DrawServerRow(
            contentUi,
            context.NameWidth,
            context.AddressWidth,
            context.PlayersWidth,
            context.PingWidth,
            server.Name,
            $"{server.Host}:{server.Port}",
            server.Players.ToString(),
            ping: null
        );

        IntRect actionRect = Widgets.Rect(context.ContentWidth - 30, 3, 24, 24);
        var options = new Widgets.IconButtonOptions(SoundEffectService: context.SoundEffectService);
        if (contentUi.IconButton($"FavoriteServer_{context.Index}", actionRect, MenuGlyphs.STAR, options))
        {
            page.AddFavorite(new SavedServer
            {
                Name = string.IsNullOrWhiteSpace(server.Name) ? server.Host : server.Name,
                Host = server.Host,
                Port = server.Port,
            });
        }
    }

    /// <summary>Everything one server row needs, so drawing it allocates no closure.</summary>
    private readonly record struct ServerRowContext(
        MultiplayerPage Page,
        bool Favorites,
        int Index,
        int NameWidth,
        int AddressWidth,
        int PlayersWidth,
        int PingWidth,
        int ContentWidth,
        SoundEffectService SoundEffectService
    );

    /// <summary>Draws the add-favorite button on the favorites tab.</summary>
    private void DrawAddFavoriteButton(UIBuilder<Material> ui, IntRect rect, int contentWidth)
    {
        if (!_favoritesTab)
        {
            return;
        }

        string label = "+ " + _localization.GetRequired("ui.button.addFavorite");
        IntRect buttonRect = Widgets.Rect(
            PANEL_PADDING,
            rect.Size.Y - PANEL_PADDING - ADD_BUTTON_HEIGHT,
            contentWidth,
            ADD_BUTTON_HEIGHT
        );
        if (!ui.ActionButton("Button_AddFavorite", buttonRect, label, ButtonVariant.Subtle))
        {
            return;
        }

        _modalMenu.GoToPage(_addFavoriteModal.Create(
            AddFavoriteFromForm,
            _networkingConfig.RemotePort.Get()
        ));
    }

    /// <summary>Draws the labels and ping indicator of one server row.</summary>
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
            _favoritesTab ? MenuGlyphs.GALAXY : MenuGlyphs.SERVER,
            Widgets.Rect(8, 6, 20, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Accent,
            Anchors.Center,
            UiTheme.FONT_ICONS
        );
        ui.Label(
            name,
            Widgets.Rect(32, 0, nameWidth - 36, ROW_HEIGHT),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            address,
            Widgets.Rect(nameWidth, 0, addressWidth - 8, ROW_HEIGHT),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            players,
            Widgets.Rect(nameWidth + addressWidth, 0, playersWidth - 8, ROW_HEIGHT),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        ui.PingIndicator(
            Widgets.Rect(nameWidth + addressWidth + playersWidth, 0, pingWidth - 8, ROW_HEIGHT),
            ping
        );
    }

    /// <summary>Draws the direct connect panel.</summary>
    private void DrawDirectConnect(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(rect))
        {
            const int labelWidth = 124;
            const int portWidth = 64;
            const int connectWidth = 92;
            const int gap = 8;
            const int rowY = 14;
            int hostX = PANEL_PADDING + labelWidth + gap;
            int hostWidth = Math.Clamp(
                rect.Size.X - hostX - portWidth - connectWidth - gap * 2 - PANEL_PADDING,
                120,
                320
            );
            int portX = hostX + hostWidth + gap;
            int connectX = portX + portWidth + gap;

            ui.Label(
                _localization.GetRequired("ui.menu.directConnect"),
                Widgets.Rect(PANEL_PADDING, rowY, labelWidth, UiMetrics.CONTROL_HEIGHT),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            Widgets.Interactions hostInteractions = ui.MenuTextBox(
                "TextBox_Host",
                ref _hostTextBox,
                Widgets.Rect(hostX, rowY, hostWidth, UiMetrics.CONTROL_HEIGHT),
                UiMetrics.MENU_SMALL_FONT,
                _inputService
            );
            HandleTextBoxInteractions(in hostInteractions, menu);

            Widgets.Interactions portInteractions = ui.MenuTextBox(
                "TextBox_Port",
                ref _portTextBox,
                Widgets.Rect(portX, rowY, portWidth, UiMetrics.CONTROL_HEIGHT),
                UiMetrics.MENU_SMALL_FONT,
                _inputService
            );
            HandleTextBoxInteractions(in portInteractions, menu);

            IntRect connectRect = Widgets.Rect(
                connectX,
                rowY,
                connectWidth,
                UiMetrics.CONTROL_HEIGHT
            );
            bool connectClicked = ui.ActionButton(
                "Button_Connect",
                connectRect,
                _localization.GetRequired("ui.button.connect"),
                ButtonVariant.Primary,
                soundEffectService: _soundEffectService
            );
            if (connectClicked)
            {
                TryConnect(menu);
            }
        }
    }

    /// <summary>Clears the error message on input and starts a connection on submit.</summary>
    private void HandleTextBoxInteractions(in Widgets.Interactions interactions, Menu<MenuPage>? menu)
    {
        if (interactions.Has(Widgets.Interactions.Input))
        {
            _errorMessage = null;
        }

        if (interactions.Has(Widgets.Interactions.Submit))
        {
            TryConnect(menu);
        }
    }

    /// <summary>Copies the selected server into the connect fields and starts a connection.</summary>
    private void JoinSelected(Menu<MenuPage> menu)
    {
        string? serverName;
        if (_favoritesTab)
        {
            if (_selectedFavorite < 0 || _selectedFavorite >= _savedServers.Count)
            {
                return;
            }

            SavedServer server = _savedServers[_selectedFavorite];
            _hostTextBox.Text.Clear().Append(server.Host);
            _portTextBox.Text.Clear().Append(server.Port.ToString());
            serverName = server.Name;
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
            serverName = server.Name;
        }

        TryConnect(menu, serverName);
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
                _discoverMessage = null;
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
                _discoverMessage = _localization.GetString("ui.notification.discover.unavailable");
            }
            else if (found.Count == 0)
            {
                _discoverMessage = _localization.GetString("ui.notification.discover.none");
            }
        }
    }

    /// <summary>Validates the connect fields, shows the connecting modal, and connects.</summary>
    private void TryConnect(Menu<MenuPage>? menu, string? serverName = null)
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

        var cts = new CancellationTokenSource();
        string targetText = !string.IsNullOrWhiteSpace(serverName) && serverName != host
            ? $"{serverName} ({host}:{port})"
            : $"{host}:{port}";

        string targetLabel = string.Format(
            _localization.GetString("ui.menu.connectingTarget") ?? "Connecting to {0}",
            targetText
        );

        _modalMenu.GoToPage(_connectingModal.Create(
            onCancel: cts.Cancel,
            target: targetLabel
        ));

        _ = Task.Run(() => TryConnectAsync(host, port, cts, menu));
    }

    /// <summary>Connects to the host and closes the connecting modal when it is done.</summary>
    private async Task TryConnectAsync(string host, int port, CancellationTokenSource cts, Menu<MenuPage>? menu)
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
                _errorMessage = _localization.GetString("ui.notification.connect.failed");
                return;
            }

            _errorMessage = null;
            _networkingConfig.RemoteHost.Set(host);
            _networkingConfig.RemotePort.Set(port);
            _networkingConfig.Save();

            await _gameSaveService.RefreshLevelsAsync();

            if (!cts.IsCancellationRequested)
            {
                menu?.GoToPage(MenuPage.SelectSave);
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
            _scrollY = 0;
        }

        PersistSavedServers();
        _errorMessage = null;
    }

    /// <summary>Asks for confirmation, then removes the favorite at the index.</summary>
    private void ConfirmRemoveFavorite(int index, string name)
    {
        _modalMenu.GoToPage(_confirmModal.Create(
            () => RemoveFavoriteAt(index),
            _localization.GetRequired("ui.menu.removeFavorite"),
            string.Format(
                _localization.GetRequired("ui.menu.removeFavoriteBody"),
                name
            ),
            _localization.GetRequired("ui.button.remove"),
            _localization.GetRequired("ui.button.cancel")
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
}
