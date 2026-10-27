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

        using (ui.Page(layout))
        {
            ui.Padding = new Padding(left: 0, top: MenuLayout.PAGE_GAP, right: 0, bottom: 0);
            ui.Spacing = MenuLayout.PAGE_GAP;

            if (_rpcHost.IsRemote)
            {
                DrawConnectionBanner(ui, menu);
            }

            DrawTabs(ui);

            int contentWidth = layout.PageWidth - PANEL_PADDING * 2 - 4;
            using (ui.MenuPanel(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                _localization.GetRequired("ui.menu.serverList"),
                padding: PANEL_PADDING
            ))
            {
                DrawServerTable(ui, contentWidth);
            }

            using (ui.MenuPanel(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(DIRECT_HEIGHT),
                },
                padding: 8
            ))
            {
                DrawDirectConnect(ui, menu);
            }

            DrawFooter(ui, menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the page footer and starts a join when asked.</summary>
    private void DrawFooter(UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(MenuLayout.FOOTER_HEIGHT),
            },
            spacing: 12
        ))
        {
            if (ui.ActionButton(
                "Button_Back",
                _localization.GetRequired("ui.button.return"),
                _returnButton.FooterButton(MenuLayout.RETURN_BUTTON_WIDTH)
            ))
            {
                menu.GoToPage(MenuPage.Home);
            }

            ui.FooterStatus(_errorMessage, UiTheme.Danger);

            int count = _favoritesTab ? _savedServers.Count : _discoveredServers.Count;
            Widgets.ActionButtonOptions joinOptions = _joinButton
                .FooterButton(MenuLayout.PRIMARY_BUTTON_WIDTH) with
            {
                Enabled = count > 0,
            };
            if (ui.ActionButton(
                "Button_JoinServer",
                _localization.GetRequired("ui.button.joinServer"),
                joinOptions
            ))
            {
                JoinSelected(menu);
            }
        }
    }

    /// <summary>Draws the favorites and LAN discovery tabs.</summary>
    private void DrawTabs(UIBuilder<Material> ui)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(TABS_HEIGHT),
            },
            spacing: 4
        ))
        {
            var tabConstraints = new Constraints
            {
                Width = new Fixed(180),
                Height = new Fill(),
            };

            if (ui.Tab(
                "FavoritesTab",
                _localization.GetRequired("ui.menu.favorites"),
                _favoritesTab,
                tabConstraints
            ))
            {
                _favoritesTab = true;
                _errorMessage = null;
                _scrollY = 0;
            }

            if (ui.Tab(
                "LanDiscoveryTab",
                _localization.GetRequired("ui.menu.lanDiscovery"),
                !_favoritesTab,
                tabConstraints
            ))
            {
                _favoritesTab = false;
                _errorMessage = null;
                _scrollY = 0;
            }
        }
    }

    /// <summary>Draws the banner that names the connected server.</summary>
    private void DrawConnectionBanner(UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Column(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(BANNER_HEIGHT),
            }
        ))
        {
            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                spacing: 8
            ))
            {
                ui.Padding = new Padding(left: PANEL_PADDING, right: PANEL_PADDING, top: 0, bottom: 0);
                ui.Color = UiTheme.PanelBackground;

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

                ui.Label(
                    MenuGlyphs.SERVER,
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.Accent,
                    new Constraints
                    {
                        Width = new Fixed(16),
                        Height = new Fill(),
                    },
                    fontId: UiTheme.FONT_ICONS
                );
                ui.Label(
                    $"{_localization.GetRequired("ui.menu.connectedTo")} {server}",
                    UiMetrics.MENU_SMALL_FONT,
                    UiTheme.Text,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    }
                );

                if (ui.ActionButton(
                    "Button_Disconnect",
                    _localization.GetRequired("ui.button.disconnect"),
                    _disconnectButton with
                    {
                        Layout = new Constraints
                        {
                            Width = new Fixed(120),
                            Height = new Fixed(UiMetrics.COMPACT_BUTTON_HEIGHT),
                            Anchors = Anchors.Center,
                        },
                    }
                ))
                {
                    DisconnectServer();
                }

                if (ui.ActionButton(
                    "Button_ViewSaves",
                    _localization.GetRequired("ui.button.viewSaves"),
                    _viewSavesButton with
                    {
                        Layout = new Constraints
                        {
                            Width = new Fixed(120),
                            Height = new Fixed(UiMetrics.COMPACT_BUTTON_HEIGHT),
                            Anchors = Anchors.Center,
                        },
                    }
                ))
                {
                    menu.GoToPage(MenuPage.SelectSave);
                }
            }

            ui.Divider(
                UiTheme.BorderInner,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(1),
                }
            );
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
    private void DrawServerTable(UIBuilder<Material> ui, int contentWidth)
    {
        const int actionWidth = 32;
        int tableWidth = contentWidth - actionWidth;
        int nameWidth = Math.Max(110, (int)(tableWidth * 0.30f));
        int addressWidth = Math.Max(130, (int)(tableWidth * 0.38f));
        int playersWidth = Math.Max(50, (int)(tableWidth * 0.12f));
        int pingWidth = Math.Max(80, tableWidth - nameWidth - addressWidth - playersWidth);

        if (!_favoritesTab)
        {
            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(24),
                },
                spacing: 8
            ))
            {
                ui.Filler();
                DrawScanButton(ui);
            }

            ui.Spacer(height: 4);
        }

        DrawServerTableHeader(ui, nameWidth, addressWidth, playersWidth, pingWidth);
        ui.Divider(
            UiTheme.Border,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
        ui.Spacer(height: 6);

        int count = _favoritesTab ? _savedServers.Count : _discoveredServers.Count;
        if (count == 0)
        {
            DrawServerTableEmptyState(ui);
        }
        else
        {
            DrawServerRows(ui, nameWidth, addressWidth, playersWidth, pingWidth, count);
        }

        DrawAddFavoriteButton(ui);

        if (count == 0)
        {
            ui.Filler();
        }
    }

    /// <summary>Draws the server table column headers.</summary>
    private void DrawServerTableHeader(
        UIBuilder<Material> ui,
        int nameWidth,
        int addressWidth,
        int playersWidth,
        int pingWidth
    )
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(16),
            }
        ))
        {
            ui.Spacer(width: 32);
            ui.Label(
                _localization.GetRequired("ui.menu.serverName"),
                UiMetrics.MENU_LABEL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fixed(Math.Max(1, nameWidth - 32)),
                    Height = new Fill(),
                }
            );
            ui.Label(
                _localization.GetRequired("ui.menu.address"),
                UiMetrics.MENU_LABEL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fixed(addressWidth),
                    Height = new Fill(),
                }
            );
            ui.Label(
                _localization.GetRequired("ui.menu.players"),
                UiMetrics.MENU_LABEL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fixed(playersWidth),
                    Height = new Fill(),
                }
            );
            ui.Label(
                _localization.GetRequired("ui.menu.ping"),
                UiMetrics.MENU_LABEL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fixed(pingWidth),
                    Height = new Fill(),
                }
            );
        }
    }

    /// <summary>Draws the LAN scan button on the discovery tab and restarts the scan.</summary>
    private void DrawScanButton(UIBuilder<Material> ui)
    {
        var options = new Widgets.ActionButtonOptions(
            Variant: ButtonVariant.Secondary,
            Enabled: !_scanning,
            Layout: new Constraints
            {
                Width = new Fixed(80),
                Height = new Fixed(22),
                Anchors = Anchors.Center,
            },
            SoundEffectService: _soundEffectService
        );
        bool clicked = ui.ActionButton(
            "Button_Scan",
            _localization.GetRequired("ui.button.scanShort"),
            options
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
    private void DrawServerTableEmptyState(UIBuilder<Material> ui)
    {
        string message = _favoritesTab
            ? _localization.GetRequired("ui.text.none")
            : _scanning
                ? _localization.GetRequired("ui.notification.discover.scanning")
                : _discoverMessage ?? _localization.GetRequired("ui.notification.discover.none");
        ui.Label(
            message,
            UiMetrics.MENU_BODY_FONT,
            _favoritesTab ? UiTheme.TextDim : UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(28),
            }
        );
    }

    /// <summary>Draws every server row inside the scroll viewport.</summary>
    private void DrawServerRows(
        UIBuilder<Material> ui,
        int nameWidth,
        int addressWidth,
        int playersWidth,
        int pingWidth,
        int count
    )
    {
        int contentHeight = count * ROW_HEIGHT + Math.Max(0, count - 1) * ROW_GAP;
        var context = new ServerRowContext(
            this,
            _favoritesTab,
            nameWidth,
            addressWidth,
            playersWidth,
            pingWidth,
            _soundEffectService
        );

        ui.ScrollArea(
            "Multiplayer.Servers",
            contentHeight,
            ref _scrollY,
            _inputService,
            body => DrawServerRowList(body, in context, count)
        );
    }

    private static void DrawServerRowList(
        UIBuilder<Material> ui,
        in ServerRowContext context,
        int count
    )
    {
        for (var index = 0; index < count; index++)
        {
            if (index > 0)
            {
                ui.Spacer(height: ROW_GAP);
            }

            bool selected = context.Favorites
                ? index == context.Page._selectedFavorite
                : index == context.Page._selectedLan;
            var options = new Widgets.SelectableCardOptions(selected);
            var rowContext = context with { Index = index };

            bool clicked = ui.SelectableCard(
                context.Favorites ? $"SavedServer_{index}" : $"Server_{index}",
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fixed(ROW_HEIGHT),
                },
                in options,
                rowContext,
                static (contentUi, row) => DrawServerRowContent(contentUi, in row)
            );
            if (!clicked)
            {
                continue;
            }

            if (context.Favorites)
            {
                context.Page._selectedFavorite = index;
            }
            else
            {
                context.Page._selectedLan = index;
            }

            context.Page._errorMessage = null;
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

        var options = new Widgets.IconButtonOptions(
            Danger: true,
            SoundEffectService: context.SoundEffectService
        );
        if (contentUi.IconButton(
            $"RemoveSavedServer_{context.Index}",
            MenuGlyphs.TRASH,
            options,
            new Constraints
            {
                Width = new Fixed(24),
                Height = new Fixed(24),
                Anchors = Anchors.Center,
            }
        ))
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

        var options = new Widgets.IconButtonOptions(SoundEffectService: context.SoundEffectService);
        if (contentUi.IconButton(
            $"FavoriteServer_{context.Index}",
            MenuGlyphs.STAR,
            options,
            new Constraints
            {
                Width = new Fixed(24),
                Height = new Fixed(24),
                Anchors = Anchors.Center,
            }
        ))
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
        int NameWidth,
        int AddressWidth,
        int PlayersWidth,
        int PingWidth,
        SoundEffectService SoundEffectService
    )
    {
        public int Index { get; init; }
    }

    /// <summary>Draws the add-favorite button on the favorites tab.</summary>
    private void DrawAddFavoriteButton(UIBuilder<Material> ui)
    {
        if (!_favoritesTab)
        {
            return;
        }

        ui.Spacer(height: 8);
        var options = new Widgets.ActionButtonOptions(
            Variant: ButtonVariant.Subtle,
            Layout: new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(ADD_BUTTON_HEIGHT),
            }
        );
        string label = "+ " + _localization.GetRequired("ui.button.addFavorite");
        if (!ui.ActionButton("Button_AddFavorite", label, options))
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
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            spacing: 4
        ))
        {
            ui.Padding = new Padding(left: 8, right: 8, top: 0, bottom: 0);

            ui.Label(
                _favoritesTab ? MenuGlyphs.GALAXY : MenuGlyphs.SERVER,
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Accent,
                new Constraints
                {
                    Width = new Fixed(20),
                    Height = new Fill(),
                },
                HorizontalAlignment.Center,
                fontId: UiTheme.FONT_ICONS
            );
            ui.Label(
                name,
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );
            ui.Label(
                address,
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fixed(Math.Max(1, addressWidth - 8)),
                    Height = new Fill(),
                }
            );
            ui.Label(
                players,
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fixed(Math.Max(1, playersWidth - 8)),
                    Height = new Fill(),
                }
            );
            ui.PingIndicator(
                new Constraints
                {
                    Width = new Fixed(Math.Max(1, pingWidth - 8)),
                    Height = new Fill(),
                },
                ping
            );
        }
    }

    /// <summary>Draws the direct connect panel.</summary>
    private void DrawDirectConnect(UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            spacing: 8
        ))
        {
            ui.Label(
                _localization.GetRequired("ui.menu.directConnect"),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fixed(124),
                    Height = new Fill(),
                }
            );

            Widgets.Interactions hostInteractions = ui.MenuTextBox(
                "TextBox_Host",
                ref _hostTextBox,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                    MaxWidth = 320,
                },
                new FontOptions { Size = UiMetrics.MENU_SMALL_FONT },
                _inputService
            );
            HandleTextBoxInteractions(in hostInteractions, menu);

            Widgets.Interactions portInteractions = ui.MenuTextBox(
                "TextBox_Port",
                ref _portTextBox,
                new Constraints
                {
                    Width = new Fixed(64),
                    Height = new Fill(),
                },
                new FontOptions { Size = UiMetrics.MENU_SMALL_FONT },
                _inputService
            );
            HandleTextBoxInteractions(in portInteractions, menu);

            var connectOptions = new Widgets.ActionButtonOptions(
                Variant: ButtonVariant.Primary,
                Layout: new Constraints
                {
                    Width = new Fixed(92),
                    Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
                    Anchors = Anchors.Center,
                },
                SoundEffectService: _soundEffectService
            );
            if (ui.ActionButton(
                "Button_Connect",
                _localization.GetRequired("ui.button.connect"),
                connectOptions
            ))
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
