using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectCharacterPage(
    in CharacterSaveManager characterSaveManager,
    in ICharacterStorage characterStorage,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in CharacterAssetService characterAssetService,
    in GameSaveManager gameSaveManager,
    in ISkillDatabase skillDatabase,
    in ModalMenu modalMenu,
    in ConfirmModal confirmModal
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectCharacter;

    private const int CARD_HEIGHT = 44;
    private const int CARD_GAP = 8;
    private const int NEW_BUTTON_HEIGHT = 32;

    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly ICharacterStorage _characterStorage = characterStorage;
    private readonly IInputService _inputService = inputService;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly ILocalization _localization = localization;
    private readonly CharacterAssetService _characterAssetService = characterAssetService;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    private readonly ISkillDatabase _skillDatabase = skillDatabase;
    private readonly ModalMenu _modalMenu = modalMenu;
    private readonly ConfirmModal _confirmModal = confirmModal;

    private readonly Widgets.ActionButtonOptions _returnButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );
    private readonly Widgets.ActionButtonOptions _launchButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Primary,
        soundEffectService
    );
    private readonly List<Character> _characters = [];
    private int _scrollY;
    private int _narrowTab;

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _characterSaveManager.ActiveSave ??= _characterSaveManager.GetMostRecentSave();

        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: true,
            leftRatio: 0.37f
        );

        using (ui.Page(layout))
        {
            ui.Padding = new Padding(left: 0, top: MenuLayout.PAGE_GAP, right: 0, bottom: 0);
            ui.Spacing = MenuLayout.PAGE_GAP;

            DrawPanels(ui, layout, menu);
            DrawFooter(ui, menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the tab strip and the panels that the current layout shows.</summary>
    private void DrawPanels(UIBuilder<Material> ui, in MenuLayout layout, Menu<MenuPage> menu)
    {
        if (!layout.UseTabs)
        {
            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                spacing: MenuLayout.PANEL_GAP
            ))
            {
                DrawManifest(
                    ui,
                    new Constraints
                    {
                        Width = new Relative(layout.LeftRatio),
                        Height = new Fill(),
                    },
                    menu
                );
                DrawDossier(
                    ui,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    },
                    showAssignment: layout.PageHeight >= 480
                );
            }

            return;
        }

        DrawTabs(ui);
        Constraints pane = new()
        {
            Width = new Fill(),
            Height = new Fill(),
        };

        if (_narrowTab == 0)
        {
            DrawManifest(ui, pane, menu);
            return;
        }

        DrawDossier(ui, pane, showAssignment: false);
    }

    /// <summary>Draws the page footer and runs the action that the player picked.</summary>
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

            ui.Filler();

            bool canLaunch = _characterSaveManager.ActiveSave != null && _gameSaveManager.ActiveSave != null;
            Widgets.ActionButtonOptions loadOptions = _launchButton
                .FooterButton(MenuLayout.PRIMARY_BUTTON_WIDTH) with
            {
                Enabled = canLaunch,
            };
            if (ui.ActionButton(
                "Button_LoadSave",
                _localization.GetRequired("ui.button.loadSave"),
                loadOptions
            ))
            {
                Task.Run(_gameSaveManager.Load);
            }
        }
    }

    private void DrawTabs(UIBuilder<Material> ui)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(MenuLayout.TABS_HEIGHT),
            },
            spacing: 8
        ))
        {
            var tabConstraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };

            if (ui.Tab(
                "ManifestTab",
                _localization.GetRequired("ui.menu.crewManifest"),
                selected: _narrowTab == 0,
                tabConstraints
            ))
            {
                _narrowTab = 0;
            }

            if (ui.Tab(
                "DossierTab",
                _localization.GetRequired("ui.menu.colonistDossier"),
                selected: _narrowTab == 1,
                tabConstraints
            ))
            {
                _narrowTab = 1;
            }
        }
    }

    private void DrawManifest(UIBuilder<Material> ui, Constraints constraints, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(constraints, _localization.GetRequired("ui.menu.crewManifest")))
        {
            RefreshCharacters();

            if (_characterSaveManager.ActiveSave == null)
            {
                _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
            }

            using (ui.Column(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            ))
            {
                if (_characters.Count > 0)
                {
                    DrawCharacterList(ui);
                }
                else
                {
                    ui.Label(
                        _localization.GetRequired("ui.text.none"),
                        UiMetrics.MENU_BODY_FONT,
                        UiTheme.TextDim,
                        new Constraints
                        {
                            Width = new Fill(),
                            Height = new Fill(),
                        },
                        HorizontalAlignment.Center
                    );
                }

                ui.Spacer(height: 8);
                DrawNewCharacterButton(ui, menu);
            }
        }
    }

    /// <summary>Draws the scrolling list of crew manifest rows.</summary>
    private void DrawCharacterList(UIBuilder<Material> ui)
    {
        int contentHeight = _characters.Count * CARD_HEIGHT
            + Math.Max(0, _characters.Count - 1) * CARD_GAP;
        Action<UIBuilder<Material>> body = contentUi => DrawCharacterCards(contentUi);

        ui.ScrollArea(
            "SelectCharacter.Manifest",
            contentHeight,
            ref _scrollY,
            _inputService,
            body
        );
    }

    /// <summary>Draws every crew manifest row inside the scroll area.</summary>
    private void DrawCharacterCards(UIBuilder<Material> ui)
    {
        Action<Character> deleteCharacter = DeleteCharacter;

        for (var i = 0; i < _characters.Count; i++)
        {
            if (i > 0)
            {
                ui.Spacer(height: CARD_GAP);
            }

            DrawCharacterCard(ui, _characters[i], deleteCharacter);
        }
    }

    /// <summary>Draws one crew manifest row and applies its selection.</summary>
    private void DrawCharacterCard(
        UIBuilder<Material> ui,
        Character character,
        Action<Character> deleteCharacter
    )
    {
        bool selected = _characterSaveManager.ActiveSave != null &&
            _characterSaveManager.ActiveSave.Value.Uuid == character.Uuid;
        var options = new Widgets.SelectableCardOptions(selected, SoundEffectService: _soundEffectService);
        var context = new CharacterCardContext(
            character,
            selected,
            _modalMenu,
            _confirmModal,
            _characterSaveManager,
            _characterAssetService,
            _soundEffectService,
            deleteCharacter
        );

        bool clicked = ui.SelectableCard(
            $"Button_SelectCharacter_{character.Uuid}",
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(CARD_HEIGHT),
            },
            in options,
            context,
            static (contentUi, state) => DrawCharacterCardContent(contentUi, state)
        );

        if (clicked)
        {
            _characterSaveManager.ActiveSave = character;
        }
    }

    /// <summary>Draws the portrait, the name, and the delete control of one crew manifest row.</summary>
    private static void DrawCharacterCardContent(UIBuilder<Material> contentUi, CharacterCardContext context)
    {
        using (contentUi.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            spacing: 8
        ))
        {
            contentUi.Padding = new Padding(left: 8, top: 0, right: 8, bottom: 0);

            Material appearance = context.CharacterAssetService.GetAppearanceMaterial(context.Character);
            using (contentUi.Image(appearance))
            {
                contentUi.Constraints = new Constraints
                {
                    Width = new Fixed(32),
                    Height = new Fixed(32),
                    Anchors = Anchors.Center,
                };
            }

            contentUi.Label(
                context.Character.Name,
                UiMetrics.MENU_BODY_FONT,
                context.Selected ? UiTheme.AccentBright : UiTheme.Text,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );

            DrawCharacterDeleteButton(contentUi, context);
        }
    }

    /// <summary>Draws the delete control of one crew manifest row and opens its confirmation.</summary>
    private static void DrawCharacterDeleteButton(UIBuilder<Material> contentUi, CharacterCardContext context)
    {
        string id = $"Button_DeleteCharacter_{context.Character.Uuid}";
        var options = new Widgets.IconButtonOptions(
            Danger: true,
            Enabled: true,
            SoundEffectService: context.SoundEffectService
        );
        bool clicked = contentUi.IconButton(
            id,
            MenuGlyphs.TRASH,
            options,
            new Constraints
            {
                Width = new Fixed(28),
                Height = new Fixed(28),
                Anchors = Anchors.Center,
            }
        );
        if (!clicked)
        {
            return;
        }

        Character character = context.Character;
        Action<Character> deleteCharacter = context.DeleteCharacter;
        context.CharacterSaveManager.ActiveSave = character;
        context.ModalMenu.GoToPage(context.ConfirmModal.Create(() => deleteCharacter(character)));
    }

    /// <summary>Deletes one character and selects the most recent remaining save.</summary>
    private void DeleteCharacter(Character character)
    {
        _characterSaveManager.Delete(character);
        _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
    }

    /// <summary>Draws the button that opens the new character page.</summary>
    private void DrawNewCharacterButton(UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        var options = new Widgets.ActionButtonOptions(
            Variant: ButtonVariant.Subtle,
            Layout: new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(NEW_BUTTON_HEIGHT),
            }
        );
        bool clicked = ui.ActionButton(
            "Button_NewCharacter",
            "+ " + _localization.GetRequired("ui.button.newCharacter"),
            options
        );
        if (clicked)
        {
            menu.GoToPage(MenuPage.NewCharacter);
        }
    }

    private void DrawDossier(UIBuilder<Material> ui, Constraints constraints, bool showAssignment)
    {
        using (ui.MenuPanel(constraints, _localization.GetRequired("ui.menu.colonistDossier")))
        {
            Character? activeCharacter = _characterSaveManager.ActiveSave;
            if (activeCharacter == null)
            {
                ui.Label(
                    _localization.GetRequired("ui.text.none"),
                    UiMetrics.MENU_BODY_FONT,
                    UiTheme.TextDim,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    },
                    HorizontalAlignment.Center
                );
                return;
            }

            Character character = activeCharacter.Value;
            DrawDossierHeader(ui, character);
            ui.Spacer(height: 16);
            DrawDossierPreview(ui, character);

            if (showAssignment)
            {
                DrawDossierAssignment(ui);
            }

            DrawDossierSummary(ui);
        }
    }

    /// <summary>Draws the name, level, and playtime readouts of the dossier.</summary>
    private void DrawDossierHeader(UIBuilder<Material> ui, in Character character)
    {
        ui.Label(
            character.Name,
            UiMetrics.MENU_TITLE_FONT,
            UiTheme.Text,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(26),
            },
            HorizontalAlignment.Center
        );

        int level = character.GetTotalSkillLevel(_skillDatabase);
        ui.Label(
            $"{_localization.GetRequired("ui.menu.level")} {level}",
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.AccentBright,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            },
            HorizontalAlignment.Center
        );

        string lastPlayed = character.Playtime.LastPlayedMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(character.Playtime.LastPlayedMs).ToLocalTime().ToString("g")
            : _localization.GetRequired("ui.label.never");
        ui.Label(
            $"{_localization.GetRequired("ui.label.lastPlayed")}: {lastPlayed}",
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            },
            HorizontalAlignment.Center
        );

        TimeSpan age = TimeSpan.FromMilliseconds(character.Playtime.AgeMs);
        ui.Label(
            $"{_localization.GetRequired("ui.label.timePlayed")}: {_localization.GetLongString(age)}",
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            },
            HorizontalAlignment.Center
        );
    }

    /// <summary>Draws the portrait and the six primary stats of the dossier.</summary>
    private void DrawDossierPreview(UIBuilder<Material> ui, in Character character)
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(128),
            },
            spacing: 20
        ))
        {
            Material appearance = _characterAssetService.GetAppearanceMaterial(character);
            using (ui.Image(appearance))
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(128),
                    Height = new Fixed(128),
                };
            }

            using (ui.Column(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            ))
            {
                DrawStats(ui, character);
            }
        }
    }

    /// <summary>Draws the six primary stat rows of the dossier.</summary>
    private void DrawStats(UIBuilder<Material> ui, in Character character)
    {
        DrawStat(ui, _localization.GetRequired("ui.field.strength"), character.Strength);
        DrawStat(ui, _localization.GetRequired("ui.field.precision"), character.Precision);
        DrawStat(ui, _localization.GetRequired("ui.field.awareness"), character.Awareness);
        DrawStat(ui, _localization.GetRequired("ui.field.charisma"), character.Charisma);
        DrawStat(ui, _localization.GetRequired("ui.field.education"), character.Education);
        DrawStat(ui, _localization.GetRequired("ui.field.resolve"), character.Resolve);
    }

    /// <summary>Draws the assignment block that tall dossier panels show.</summary>
    private void DrawDossierAssignment(UIBuilder<Material> ui)
    {
        ui.Spacer(height: 16);
        ui.Divider(
            UiTheme.Border,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
        ui.Spacer(height: 8);
        ui.Label(
            _localization.GetRequired("ui.menu.assignment"),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        );
        ui.Label(
            _gameSaveManager.ActiveSave?.Name ?? _localization.GetRequired("ui.text.none"),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.Text,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(22),
            }
        );
    }

    /// <summary>Draws the launch summary at the bottom of the dossier.</summary>
    private void DrawDossierSummary(UIBuilder<Material> ui)
    {
        ui.Filler();
        ui.Divider(
            UiTheme.Border,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
        ui.Spacer(height: 8);
        ui.Label(
            _localization.GetRequired("ui.menu.readyForLaunch"),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Success,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(20),
            }
        );
    }

    private static void DrawStat(
        UIBuilder<Material> ui,
        string label,
        int value
    )
    {
        using (ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(18),
            }
        ))
        {
            ui.StatLabel(
                label,
                UiMetrics.MENU_SMALL_FONT,
                focused: false,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );
            ui.Label(
                value.ToString(),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                new Constraints
                {
                    Width = new Fixed(28),
                    Height = new Fill(),
                },
                HorizontalAlignment.Right
            );
        }
    }

    /// <summary>Reloads the cached character list, ordered by the most recent play first.</summary>
    private void RefreshCharacters()
    {
        _characters.Clear();
        _characters.AddRange(_characterStorage.GetAllCharacters());

        //  Stable insertion sort: the most recently played character leads, and equal
        //  timestamps keep their storage order. List.Sort is not stable, and LINQ is not free here.
        for (var i = 1; i < _characters.Count; i++)
        {
            Character current = _characters[i];
            var j = i - 1;
            while (j >= 0 && _characters[j].Playtime.LastPlayedMs < current.Playtime.LastPlayedMs)
            {
                _characters[j + 1] = _characters[j];
                j--;
            }

            _characters[j + 1] = current;
        }
    }

    /// <summary>Everything one crew manifest row needs to draw itself.</summary>
    private readonly record struct CharacterCardContext(
        Character Character,
        bool Selected,
        ModalMenu ModalMenu,
        ConfirmModal ConfirmModal,
        CharacterSaveManager CharacterSaveManager,
        CharacterAssetService CharacterAssetService,
        SoundEffectService SoundEffectService,
        Action<Character> DeleteCharacter
    );
}
