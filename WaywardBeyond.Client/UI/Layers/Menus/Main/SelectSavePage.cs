using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
using WaywardBeyond.Config;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectSavePage(
    in GameSaveManager gameSaveManager,
    in GameSaveService gameSaveService,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in ICharacterStorage characterStorage,
    in ModalMenu modalMenu,
    in ConfirmModal confirmModal,
    in TransportManager transportManager,
    in NetworkingSettings networkingSettings,
    in ProfileSettings profileSettings
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectSave;
    
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly GameSaveService _gameSaveService = gameSaveService;
    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;
    private readonly ICharacterStorage _characterStorage = characterStorage;
    private readonly ModalMenu _modalMenu = modalMenu;
    private readonly ConfirmModal _confirmModal = confirmModal;
    private readonly TransportManager _transportManager = transportManager;
    private readonly NetworkingSettings _networkingSettings = networkingSettings;
    private readonly ProfileSettings _profileSettings = profileSettings;

    private const int RefreshIntervalMs = 1000;

    private bool _remoteConnectTried;
    private long _lastRenderMs;
    private long _lastRefreshMs;

    private readonly Widgets.ButtonOptions _menuButtonOptions = new(
        new FontOptions 
        {
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );

    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions
        {
            Size = 20,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
    
    private readonly Widgets.ButtonOptions _iconOptions = new(
        new FontOptions {
            ID = "Font Awesome 6 Free Solid",
            Size = 32,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
        
    private readonly Widgets.ButtonOptions _smallIconOptions = new(
        new FontOptions {
            ID = "Font Awesome 6 Free Solid",
            Size = 20,
        },
        new Widgets.AudioOptions(soundEffectService)
    );
    
    private int _scrollY;

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
                ui.FontSize = 24;
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
                    ui.FontOptions = _buttonOptions.FontOptions;
                    ui.Color = new Vector4(0.325f, 0.325f, 0.325f, 1f);
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
                        
                        foreach (GameSave save in saves.OrderByDescending(save => save.Level.LastPlayedMs))
                        {
                            using (ui.TextButton(id: $"Button_DeleteSave_{save.Name}", text: "\uf2ed", _smallIconOptions, out Widgets.Interactions interactions))
                            {
                                if (interactions.Has(Widgets.Interactions.Click))
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
                    }
                    
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Vertical;
                        
                        foreach (GameSave save in saves.OrderByDescending(save => save.Level.LastPlayedMs))
                        {
                            using (ui.TextButton(id: $"Button_SelectSave_{save.Name}", text: save.Name, _buttonOptions, out Widgets.Interactions interactions))
                            {
                                if (_gameSaveManager.ActiveSave != null && _gameSaveManager.ActiveSave.Value.Name == save.Name)
                                {
                                    ui.Color = new Vector4(0f, 0.455f, 1f, 0.5f);
                                }

                                if (interactions.Has(Widgets.Interactions.Click))
                                {
                                    _gameSaveManager.ActiveSave = save;
                                }
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

                    string lastPlayedStr = activeSave.Level.LastPlayedMs > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds(activeSave.Level.LastPlayedMs).ToLocalTime().ToString(format: "g")
                        : _localization.GetString("ui.label.never")!;
                    using (ui.Text(_localization.GetString("ui.label.lastPlayed")!)) { }
                    using (ui.Text(lastPlayedStr))
                    {
                        ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                    }
                }

                TimeSpan age = TimeSpan.FromMilliseconds(activeSave.Level.AgeMs);
                string timePlayedStr = _localization.GetLongString(age);
                using (ui.Element())
                {
                    ui.Spacing = 8;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                    
                    using (ui.Text(_localization.GetString("ui.label.timePlayed")!)) { }
                    using (ui.Text(timePlayedStr))
                    {
                        ui.Color = new Vector4(0.75f, 0.75f, 0.75f, 1f);
                    }
                }
            }
            
            using (ui.TextButton(id: "Button_Next", text: _localization.GetString("ui.button.next")!, _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
            
                if (interactions.Has(Widgets.Interactions.Click))
                {
                    IEnumerable<Character> characters = _characterStorage.GetAllCharacters();
                    menu.GoToPage(characters.Any() ? MenuPage.Characters : MenuPage.NewCharacter);
                }
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
            
            using (ui.TextButton(id: "Button_Back", text: _localization.GetString("ui.button.back")!, _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                if (interactions.Has(Widgets.Interactions.Click))
                {
                    menu.GoBack();
                }
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
            || _transportManager.Active != null
            || _remoteConnectTried)
        {
            return;
        }

        _remoteConnectTried = true;
        Result result = _transportManager.ConnectRemote(_networkingSettings.DefaultHost.Get(), _networkingSettings.DefaultConnectPort.Get());
        if (result.Success)
        {
            _ = _gameSaveService.RefreshLevelsAsync();
        }
    }
}