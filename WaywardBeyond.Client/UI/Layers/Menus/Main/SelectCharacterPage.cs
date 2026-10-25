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
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class SelectCharacterPage(
    in CharacterSaveManager characterSaveManager,
    in ICharacterStorage characterStorage,
    in IInputService inputService,
    in SoundEffectService soundEffectService,
    in ILocalization localization,
    in CharacterAssetService characterAssetService,
    in GameSaveManager gameSaveManager,
    in ModalMenu modalMenu,
    in ConfirmModal confirmModal
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.SelectCharacter;

    private const int CARD_HEIGHT = 44;
    private const int CARD_GAP = 8;
    private const int PANEL_PADDING = 12;
    private const int NEW_BUTTON_HEIGHT = 32;

    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly ICharacterStorage _characterStorage = characterStorage;
    private readonly IInputService _inputService = inputService;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly ILocalization _localization = localization;
    private readonly CharacterAssetService _characterAssetService = characterAssetService;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
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

            if (layout.UseTabs)
            {
                DrawTabs(ui, layout);
                if (_narrowTab == 0)
                {
                    DrawManifest(ui, layout.Right, menu);
                }
                else
                {
                    DrawDossier(ui, layout.Right, menu);
                }
            }
            else
            {
                DrawManifest(ui, layout.Left, menu);
                DrawDossier(ui, layout.Right, menu);
            }

            if (ui.ActionButton(
                    "Button_Back",
                    _localization.GetString("ui.button.return")!,
                    _returnButton with { Rect = layout.ReturnButton }
                ))
            {
                menu.GoToPage(MenuPage.Home);
            }

            bool canLaunch = _characterSaveManager.ActiveSave != null && _gameSaveManager.ActiveSave != null;
            if (ui.ActionButton(
                    "Button_LoadSave",
                    _localization.GetString("ui.button.loadSave")!,
                    _launchButton with { Rect = layout.PrimaryButton, Enabled = canLaunch }
                ))
            {
                Task.Run(_gameSaveManager.Load);
            }
        }

        return Result.FromSuccess();
    }

    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;
        if (ui.Tab(
                "ManifestTab",
                Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.crewManifest")!,
                selected: _narrowTab == 0
            ))
        {
            _narrowTab = 0;
        }

        if (ui.Tab(
                "DossierTab",
                Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.colonistDossier")!,
                selected: _narrowTab == 1
            ))
        {
            _narrowTab = 1;
        }
    }

    private void DrawManifest(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(rect, _localization.GetString("ui.menu.crewManifest")!))
        {
            List<Character> characters = _characterStorage.GetAllCharacters()
                .OrderByDescending(character => character.Playtime.LastPlayedMs)
                .ToList();

            if (_characterSaveManager.ActiveSave == null)
            {
                _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
            }

            int contentWidth = Math.Max(1, rect.Size.X - PANEL_PADDING * 2);
            int listY = 38;
            int listHeight = Math.Max(0, rect.Size.Y - listY - NEW_BUTTON_HEIGHT - PANEL_PADDING - 8);

            if (characters.Count == 0)
            {
                ui.Label(
                    _localization.GetString("ui.text.none")!,
                    Widgets.Rect(PANEL_PADDING, listY + 12, contentWidth, 20),
                    UiMetrics.MenuBodyFont,
                    UiTheme.TextDim,
                    Anchors.Center
                );
            }
            else
            {
                IntRect listRect = Widgets.Rect(PANEL_PADDING, listY, contentWidth, listHeight);
                using (ui.Element())
                {
                    ui.LayoutDirection = LayoutDirection.None;
                    ui.Color = UiTheme.Transparent;
                    ui.Constraints = Widgets.RectConstraints(listRect);
                    ui.ClipConstraints = new Constraints
                    {
                        Width = new Fixed(listRect.Size.X),
                        Height = new Fixed(listRect.Size.Y),
                    };

                    int contentHeight = characters.Count * (CARD_HEIGHT + CARD_GAP);
                    int maxScroll = Math.Max(0, contentHeight - listHeight);
                    float wheel = _inputService.GetMouseScroll();
                    if (wheel != 0f)
                    {
                        _scrollY -= (int)MathF.Round(wheel * UiMetrics.ScrollStep);
                    }
                    _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
                    ui.ScrollY = -_scrollY;

                    for (var i = 0; i < characters.Count; i++)
                    {
                        Character character = characters[i];
                        bool selected = _characterSaveManager.ActiveSave != null &&
                            _characterSaveManager.ActiveSave.Value.Uuid == character.Uuid;
                        int cardY = i * (CARD_HEIGHT + CARD_GAP);
                        IntRect cardRect = Widgets.Rect(0, cardY, contentWidth - 8, CARD_HEIGHT);

                        bool clicked = ui.SelectableCard(
                            $"Button_SelectCharacter_{character.Uuid}",
                            cardRect,
                            new Widgets.SelectableCardOptions(selected, SoundEffectService: _soundEffectService),
                            contentUi =>
                            {
                                Material appearance = _characterAssetService.GetAppearanceMaterial(character);
                                using (contentUi.Image(appearance))
                                {
                                    contentUi.Constraints = new Constraints
                                    {
                                        X = new Fixed(6),
                                        Y = new Fixed(6),
                                        Width = new Fixed(32),
                                        Height = new Fixed(32),
                                    };
                                }

                                contentUi.Label(
                                    character.Name,
                                    Widgets.Rect(44, 0, Math.Max(1, contentWidth - 90), CARD_HEIGHT),
                                    UiMetrics.MenuBodyFont,
                                    selected ? UiTheme.AccentBright : UiTheme.Text,
                                    Anchors.Left | Anchors.Center
                                );

                                bool canDelete = characters.Count > 1;
                                if (contentUi.IconButton(
                                        $"Button_DeleteCharacter_{character.Uuid}",
                                        Widgets.Rect(contentWidth - 42, 8, 28, 28),
                                        MenuGlyphs.Trash,
                                        new Widgets.IconButtonOptions(
                                            Danger: true,
                                            Enabled: canDelete,
                                            SoundEffectService: _soundEffectService
                                        )
                                    ))
                                {
                                    _characterSaveManager.ActiveSave = character;
                                    _modalMenu.GoToPage(_confirmModal.Create(DeleteCharacter));
                                }

                                void DeleteCharacter()
                                {
                                    _characterSaveManager.Delete(character);
                                    _characterSaveManager.ActiveSave = _characterSaveManager.GetMostRecentSave();
                                }
                            }
                        );

                        if (clicked)
                        {
                            _characterSaveManager.ActiveSave = character;
                        }
                    }
                }

                ui.Scrollbar(
                    Widgets.Rect(rect.Size.X - PANEL_PADDING - 6, listY, 6, listHeight),
                    characters.Count * (CARD_HEIGHT + CARD_GAP),
                    listHeight,
                    _scrollY
                );
            }

            IntRect newButtonRect = Widgets.Rect(
                PANEL_PADDING,
                rect.Size.Y - NEW_BUTTON_HEIGHT - PANEL_PADDING,
                contentWidth,
                NEW_BUTTON_HEIGHT
            );
            if (ui.ActionButton(
                    "Button_NewCharacter",
                    newButtonRect,
                    "+ " + _localization.GetString("ui.button.newCharacter")!,
                    ButtonVariant.Subtle
                ))
            {
                menu.GoToPage(MenuPage.NewCharacter);
            }
        }
    }

    private void DrawDossier(UIBuilder<Material> ui, IntRect rect, Menu<MenuPage> menu)
    {
        using (ui.MenuPanel(rect, _localization.GetString("ui.menu.colonistDossier")!))
        {
            Character? activeCharacter = _characterSaveManager.ActiveSave;
            if (activeCharacter == null)
            {
                ui.Label(
                    _localization.GetString("ui.text.none")!,
                    Widgets.Rect(0, 44, rect.Size.X, 24),
                    UiMetrics.MenuBodyFont,
                    UiTheme.TextDim,
                    Anchors.Center
                );
                return;
            }

            Character character = activeCharacter.Value;
            int padding = 16;

            ui.Label(
                character.Name,
                Widgets.Rect(padding, 38, rect.Size.X - padding * 2, 26),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );

            string lastPlayed = character.Playtime.LastPlayedMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(character.Playtime.LastPlayedMs).ToLocalTime().ToString("g")
                : _localization.GetString("ui.label.never")!;
            ui.Label(
                $"{_localization.GetString("ui.label.lastPlayed")!}: {lastPlayed}",
                Widgets.Rect(padding, 64, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Center
            );

            TimeSpan age = TimeSpan.FromMilliseconds(character.Playtime.AgeMs);
            ui.Label(
                $"{_localization.GetString("ui.label.timePlayed")!}: {_localization.GetLongString(age)}",
                Widgets.Rect(padding, 82, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Center
            );

            int previewY = 116;
            Material appearance = _characterAssetService.GetAppearanceMaterial(character);
            using (ui.Image(appearance))
            {
                ui.Constraints = new Constraints
                {
                    X = new Fixed(padding),
                    Y = new Fixed(previewY),
                    Width = new Fixed(128),
                    Height = new Fixed(128),
                };
            }

            int statsX = padding + 148;
            DrawStat(ui, "S", _localization.GetString("ui.field.strength")!, character.Strength, statsX, previewY);
            DrawStat(ui, "P", _localization.GetString("ui.field.precision")!, character.Precision, statsX, previewY + 24);
            DrawStat(ui, "A", _localization.GetString("ui.field.awareness")!, character.Awareness, statsX, previewY + 48);
            DrawStat(ui, "C", _localization.GetString("ui.field.charisma")!, character.Charisma, statsX, previewY + 72);
            DrawStat(ui, "E", _localization.GetString("ui.field.education")!, character.Education, statsX, previewY + 96);
            DrawStat(ui, "R", _localization.GetString("ui.field.resolve")!, character.Resolve, statsX, previewY + 120);

            int total = character.Strength + character.Precision + character.Awareness
                + character.Charisma + character.Education + character.Resolve;
            if (rect.Size.Y > 400)
            {
                ui.Divider(
                    Widgets.Rect(padding, 280, rect.Size.X - padding * 2, 1),
                    UiTheme.Border
                );
                ui.Label(
                    _localization.GetString("ui.menu.assignment")!,
                    Widgets.Rect(padding, 292, rect.Size.X - padding * 2, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.TextMuted,
                    Anchors.Left | Anchors.Center
                );
                ui.Label(
                    _gameSaveManager.ActiveSave?.Name ?? _localization.GetString("ui.text.none")!,
                    Widgets.Rect(padding, 312, rect.Size.X - padding * 2, 22),
                    UiMetrics.MenuBodyFont,
                    UiTheme.Text,
                    Anchors.Left | Anchors.Center
                );
            }

            int summaryY = Math.Max(previewY + 164, rect.Size.Y - 96);
            ui.Divider(
                Widgets.Rect(padding, summaryY, rect.Size.X - padding * 2, 1),
                UiTheme.Border
            );
            ui.Label(
                _localization.GetString("ui.menu.spacerTotal")!,
                Widgets.Rect(padding, summaryY + 12, 200, 20),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                total.ToString(),
                Widgets.Rect(rect.Size.X - padding - 120, summaryY + 12, 120, 20),
                UiMetrics.MenuBodyFont,
                UiTheme.AccentBright,
                Anchors.Right | Anchors.Center
            );
            ui.Label(
                _localization.GetString("ui.menu.readyForLaunch")!,
                Widgets.Rect(padding, summaryY + 38, rect.Size.X - padding * 2, 20),
                UiMetrics.MenuSmallFont,
                UiTheme.Success,
                Anchors.Left | Anchors.Center
            );
        }
    }

    private static void DrawStat(
        UIBuilder<Material> ui,
        string shortName,
        string label,
        int value,
        int x,
        int y
    )
    {
        using (ui.Element())
        {
            ui.Color = UiTheme.ControlBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = UiTheme.BorderHover,
            };
            ui.Constraints = Widgets.RectConstraints(Widgets.Rect(x, y, 18, 18));
        }

        ui.Label(
            shortName,
            Widgets.Rect(x, y, 18, 18),
            UiMetrics.MenuSmallFont,
            UiTheme.Text,
            Anchors.Center
        );
        ui.Label(
            label,
            Widgets.Rect(x + 26, y, 96, 18),
            UiMetrics.MenuSmallFont,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            value.ToString(),
            Widgets.Rect(x + 124, y, 28, 18),
            UiMetrics.MenuSmallFont,
            UiTheme.Text,
            Anchors.Right | Anchors.Center
        );
    }
}
