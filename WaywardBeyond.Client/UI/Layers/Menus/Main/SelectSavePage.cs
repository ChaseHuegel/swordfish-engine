using System;
using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.Constraints;
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
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectSavePage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectSave;

    private readonly GameSaveManager _gameSaveManager;
    private readonly GameSaveService _gameSaveService;
    private readonly IInputService _inputService;
    private readonly ILocalization _localization;
    private readonly ICharacterStorage _characterStorage;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly ClientRpcHost _rpcHost;
    private readonly NetworkingConfig _networkingConfig;
    private readonly ProfileSettings _profileSettings;

    private const int RefreshIntervalMs = 1000;

    private bool _remoteConnectTried;
    private long _lastRenderMs;
    private long _lastRefreshMs;

    private readonly Widgets.ActionButtonOptions _primaryButton;
    private readonly Widgets.ActionButtonOptions _subtleButton;
    private readonly Widgets.ActionButtonOptions _saveRowButton;
    private readonly Widgets.ActionButtonOptions _deleteIconButton;

    private int _scrollY;

    public SelectSavePage(
        in GameSaveManager gameSaveManager,
        in GameSaveService gameSaveService,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in ICharacterStorage characterStorage,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal,
        in ClientRpcHost rpcHost,
        in NetworkingConfig networkingConfig,
        in ProfileSettings profileSettings
    ) {
        _gameSaveManager = gameSaveManager;
        _gameSaveService = gameSaveService;
        _inputService = inputService;
        _localization = localization;
        _characterStorage = characterStorage;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;
        _rpcHost = rpcHost;
        _networkingConfig = networkingConfig;
        _profileSettings = profileSettings;

        _primaryButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _subtleButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Subtle, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _saveRowButton = Widgets.ActionButtonOptions.ListRow(ButtonVariant.Secondary, soundEffectService);
        _deleteIconButton = Widgets.ActionButtonOptions.Icon(
            ButtonVariant.Danger,
            soundEffectService,
            size: UiMetrics.SmallIconButtonSize,
            fontSize: UiMetrics.IconFont,
            fontId: "Font Awesome 6 Free Solid"
        );
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        EnsureRemoteConnection();
        RefreshSaveListIfDue();

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString("ui.menu.saves")!))
            {
                ui.FontSize = UiMetrics.TitleFont;
                ui.Color = UiTheme.Text;
            }
        }

        using (ui.Element("saves"))
        {
            ui.VerticalScroll = true;

            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Height = new Fixed(196),
            };

            ui.ClipConstraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            };

            if (_gameSaveManager.ActiveSave == null)
            {
                _gameSaveManager.ActiveSave = _gameSaveManager.GetMostRecentSave();
            }

            GameSave[] saves = _gameSaveService.GetSaves();
            if (saves.Length == 0)
            {
                using (ui.Text(_localization.GetString("ui.text.none")!))
                {
                    ui.FontSize = UiMetrics.BodyFont;
                    ui.Color = UiTheme.TextDim;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                }
            }
            else
            {
                float scroll = _inputService.GetMouseScroll();
                _scrollY = Math.Clamp(_scrollY + (int)scroll, -(saves.Length - 1), 0);
                ui.ScrollY = _scrollY * 30;

                using (ui.Element())
                {
                    ui.Spacing = 20;

                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;
                        ui.Spacing = 12;

                        foreach (GameSave save in saves.OrderByDescending(save => save.Level.Playtime.LastPlayedMs))
                        {
                            if (ui.ActionButton($"Button_DeleteSave_{save.Name}", "\uf2ed", _deleteIconButton))
                            {
                                _modalMenu.GoToPage(_confirmModal.Create(DeleteSave));

                                void DeleteSave()
                                {
                                    _gameSaveManager.Delete(save);
                                    _gameSaveManager.ActiveSave = _gameSaveManager.GetMostRecentSave();
                                }
                            }
                        }
                    }

                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;

                        foreach (GameSave save in saves.OrderByDescending(save => save.Level.Playtime.LastPlayedMs))
                        {
                            bool selected = _gameSaveManager.ActiveSave != null && _gameSaveManager.ActiveSave.Value.Name == save.Name;
                            Widgets.ActionButtonOptions options = _saveRowButton with
                            {
                                Selected = selected,
                            };

                            if (ui.ActionButton($"Button_SelectSave_{save.Name}", save.Name, options))
                            {
                                _gameSaveManager.ActiveSave = save;
                            }
                        }
                    }
                }
            }
        }

        if (_gameSaveManager.ActiveSave != null)
        {
            GameSave activeSave = _gameSaveManager.ActiveSave.Value;

            using (ui.Element())
            {
                ui.Spacing = 8;
                ui.Padding = new Padding(16);
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    string lastPlayedStr = activeSave.Level.Playtime.LastPlayedMs > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds(activeSave.Level.Playtime.LastPlayedMs).ToLocalTime().ToString(format: "g")
                        : _localization.GetString("ui.label.never")!;

                    using (ui.Text($"{_localization.GetString("ui.label.lastPlayed")!} #BFBFBFFF {lastPlayedStr}")) { }
                }

                TimeSpan age = TimeSpan.FromMilliseconds(activeSave.Level.Playtime.AgeMs);
                string timePlayedStr = _localization.GetLongString(age);
                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };

                    using (ui.Text($"{_localization.GetString("ui.label.timePlayed")!} #BFBFBFFF {timePlayedStr}")) { }
                }
            }

            if (ui.ActionButton("Button_Next", _localization.GetString("ui.button.next")!, _primaryButton))
            {
                IEnumerable<Character> characters = _characterStorage.GetAllCharacters();
                menu.GoToPage(characters.Any() ? MenuPage.Characters : MenuPage.NewCharacter);
            }
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.back")!, _subtleButton))
            {
                menu.GoBack();
            }
        }

        return Result.FromSuccess();
    }

    /// <summary>
    /// Pulls the save listing so the page reflects the server's current levels. <see cref="RenderPage"/>
    /// only runs while this page is current, so a gap since the last render means the page was just
    /// (re)entered and must refresh at once. While the page stays open it re-pulls on the throttle, which
    /// is what surfaces levels another client created.
    /// </summary>
    private void RefreshSaveListIfDue()
    {
        long now = Environment.TickCount64;

        bool entered = now - _lastRenderMs > 250;
        _lastRenderMs = now;

        if (!entered && now - _lastRefreshMs < RefreshIntervalMs)
        {
            return;
        }

        _lastRefreshMs = now;
        _ = _gameSaveService.RefreshLevelsAsync();
    }

    /// <summary>
    /// Continue branch: when the last session was remote and no transport is active, connect to the
    /// persisted endpoint so the save list and join flow target that server. A local session needs no
    /// action - the in-process host server is already running.
    /// </summary>
    private void EnsureRemoteConnection()
    {
        if (_profileSettings.LastServerMode.Get() != LastServerMode.Remote
            || _rpcHost.IsRemote
            || _remoteConnectTried)
        {
            return;
        }

        _remoteConnectTried = true;
        Result result = _rpcHost.ConnectRemote(_networkingConfig.RemoteHost.Get(), _networkingConfig.RemotePort.Get());
        if (result.Success)
        {
            _ = _gameSaveService.RefreshLevelsAsync();
        }
    }
}
