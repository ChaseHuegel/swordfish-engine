using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private const int DEFAULT_SPACER_POINTS = 18;
    private const int MIN_STAT = 1;
    private const int MAX_STAT = 8;

    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewCharacter;

    private readonly ILogger<NewCharacterPage> _logger;
    private readonly ICharacterStorage _characterStorage;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly CharacterAssetService _characterAssetService;
    private readonly NameGenerator _nameGenerator;
    private readonly NotificationService _notificationService;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;

    private readonly Widgets.ActionButtonOptions _backButton;
    private readonly Widgets.ActionButtonOptions _createButton;
    private readonly Randomizer _randomizer = new();

    private int _characterMaterialIndex;
    private TextBoxState _nameTextBox;
    private int _spacerPoints = DEFAULT_SPACER_POINTS;
    private int _focusedStat;
    private int _narrowTab;
    private int _strength = 1;
    private int _precision = 1;
    private int _awareness = 1;
    private int _charisma = 1;
    private int _education = 1;
    private int _resolve = 1;

    public NewCharacterPage(
        in ILogger<NewCharacterPage> logger,
        in ICharacterStorage characterStorage,
        in CharacterSaveManager characterSaveManager,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in CharacterAssetService characterAssetService,
        in NameGenerator nameGenerator,
        in NotificationService notificationService,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal
    ) {
        _logger = logger;
        _characterStorage = characterStorage;
        _characterSaveManager = characterSaveManager;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _characterAssetService = characterAssetService;
        _nameGenerator = nameGenerator;
        _notificationService = notificationService;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;

        _backButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
        _createButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);
        var nameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.characterName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fixed(300),
                Height = new Fixed(UiMetrics.ControlHeight),
            }
        );
        _nameTextBox = new TextBoxState(initialValue: string.Empty, options: nameTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: true,
            leftRatio: 0.5f
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
                    DrawIdentity(ui, layout.Right);
                }
                else
                {
                    DrawEvaluation(ui, layout.Right);
                }
            }
            else
            {
                DrawIdentity(ui, layout.Left);
                DrawEvaluation(ui, layout.Right);
            }

            if (ui.ActionButton(
                    "Button_Back",
                    _localization.GetString("ui.button.back")!,
                    _backButton with { Rect = layout.ReturnButton }
                ))
            {
                menu.GoToPage(MenuPage.SelectCharacter);
            }

            string currentName = CharacterNames.Clamp(_nameTextBox.Text.ToString().Trim(_characterNameTrimChars));
            bool canCreate = !string.IsNullOrWhiteSpace(currentName)
                && !_characterStorage.GetAllCharacters().Any(character => character.Name == currentName);
            if (ui.ActionButton(
                    "Button_CreateCharacter",
                    _localization.GetString("ui.button.createCharacter")!,
                    _createButton with { Rect = layout.PrimaryButton, Enabled = canCreate }
                ))
            {
                if (_spacerPoints > 0)
                {
                    _modalMenu.GoToPage(_confirmModal.Create(
                        () => TryCreate(menu),
                        _localization.GetString("ui.menu.unspentPoints")!,
                        string.Format(
                            _localization.GetString("ui.menu.unspentPointsBody")!,
                            _spacerPoints
                        ),
                        _localization.GetString("ui.button.createCharacter")!,
                        _localization.GetString("ui.button.cancel")!
                    ));
                }
                else
                {
                    TryCreate(menu);
                }
            }
        }

        return Result.FromSuccess();
    }

    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;
        if (ui.Tab(
                "IdentityTab",
                Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.colonistIdentity")!,
                selected: _narrowTab == 0
            ))
        {
            _narrowTab = 0;
        }

        if (ui.Tab(
                "EvaluationTab",
                Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.spacerEvaluation")!,
                selected: _narrowTab == 1
            ))
        {
            _narrowTab = 1;
        }
    }

    private void DrawIdentity(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetString("ui.menu.colonistIdentity")!))
        {
            int padding = 20;
            const int inputWidth = 300;
            int nameGroupWidth = inputWidth + 8 + UiMetrics.SmallIconButtonSize;
            int nameGroupX = (rect.Size.X - nameGroupWidth) / 2;

            ui.Label(
                _localization.GetString("ui.field.characterNameLabel")!,
                Widgets.Rect(nameGroupX, 38, nameGroupWidth, 16),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            using (ui.Element())
            {
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(nameGroupX, 54, inputWidth, UiMetrics.ControlHeight));
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(inputWidth),
                    Height = new Fixed(UiMetrics.ControlHeight),
                };
                ui.TextBox("TextBox_CharacterName", ref _nameTextBox, new FontOptions { Size = UiMetrics.MenuBodyFont }, _inputService, _soundEffectService);
            }

            if (ui.IconButton(
                    "Button_RandomName",
                    Widgets.Rect(nameGroupX + inputWidth + 8, 54, UiMetrics.SmallIconButtonSize, UiMetrics.SmallIconButtonSize),
                    MenuGlyphs.Dice,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ))
            {
                RandomizeName();
            }

            int boxWidth = 208;
            int boxHeight = 208;
            int boxX = (rect.Size.X - boxWidth) / 2;
            int boxY = 96;
            using (ui.Element())
            {
                ui.Color = UiTheme.PanelInner;
                ui.Border = new BorderStyle
                {
                    Thickness = UiMetrics.PanelBorderThickness,
                    Color = UiTheme.Border,
                };
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(boxX, boxY, boxWidth, boxHeight));
            }

            Material appearance = _characterAssetService.GetAppearanceMaterial(
                _characterAssetService.GetBodyId(_characterMaterialIndex)
            );
            using (ui.Image(appearance))
            {
                ui.Constraints = new Constraints
                {
                    X = new Fixed(boxX + (boxWidth - 192) / 2),
                    Y = new Fixed(boxY + 8),
                    Width = new Fixed(192),
                    Height = new Fixed(192),
                };
            }

            if (ui.IconButton(
                    "Button_PreviousCharacter",
                    Widgets.Rect(boxX - 40, boxY + boxHeight / 2 - 16, 32, 32),
                    MenuGlyphs.ChevronLeft,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ))
            {
                CycleBody(-1);
            }

            if (ui.IconButton(
                    "Button_NextCharacter",
                    Widgets.Rect(boxX + boxWidth + 8, boxY + boxHeight / 2 - 16, 32, 32),
                    MenuGlyphs.ChevronRight,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ))
            {
                CycleBody(1);
            }

            if (ui.IconButton(
                    "Button_RandomCharacter",
                    Widgets.Rect(boxX + boxWidth + 48, boxY + boxHeight / 2 - 16, 32, 32),
                    MenuGlyphs.Dice,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ) && _characterAssetService.GetAppearancesCount() > 0)
            {
                _characterMaterialIndex = _randomizer.NextInt(0, _characterAssetService.GetAppearancesCount());
            }

            ui.Label(
                _localization.GetString("ui.field.bodyPreset")!,
                Widgets.Rect(padding, boxY + boxHeight + 14, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Center
            );
            ui.Label(
                FormatBodyId(_characterAssetService.GetBodyId(_characterMaterialIndex)),
                Widgets.Rect(padding, boxY + boxHeight + 34, rect.Size.X - padding * 2, 24),
                UiMetrics.MenuBodyFont,
                UiTheme.AccentBright,
                Anchors.Center
            );

            if (rect.Size.Y > 430)
            {
                ui.Paragraph(
                    _localization.GetString("ui.menu.bodyPresetInfo")!,
                    Widgets.Rect(padding, rect.Size.Y - 64, rect.Size.X - padding * 2, 60),
                    UiMetrics.MenuSmallFont,
                    UiTheme.TextMuted
                );
            }
        }
    }

    private void DrawEvaluation(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetString("ui.menu.spacerEvaluation")!))
        {
            int padding = 16;

            ui.Paragraph(
                _localization.GetString("ui.field.spacer.description")!,
                Widgets.Rect(padding, 38, rect.Size.X - padding * 2, 36),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted
            );

            ui.Label(
                _localization.GetString("ui.field.spacer.points")!,
                Widgets.Rect(padding, 84, rect.Size.X - padding * 2 - 48, 20),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                _spacerPoints.ToString(),
                Widgets.Rect(rect.Size.X - padding - 76, 84, 36, 20),
                UiMetrics.MenuBodyFont,
                UiTheme.Primary,
                Anchors.Right | Anchors.Center
            );
            if (ui.IconButton(
                    "Button_RandomPoints",
                    Widgets.Rect(rect.Size.X - padding - 32, 78, 32, 32),
                    MenuGlyphs.Dice,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ))
            {
                RandomizePoints();
            }

            int rowY = 112;
            DrawStatRow(ui, rect, 0, _localization.GetString("ui.field.strength")!, _strength, padding, rowY);
            DrawStatRow(ui, rect, 1, _localization.GetString("ui.field.precision")!, _precision, padding, rowY + 28);
            DrawStatRow(ui, rect, 2, _localization.GetString("ui.field.awareness")!, _awareness, padding, rowY + 56);
            DrawStatRow(ui, rect, 3, _localization.GetString("ui.field.charisma")!, _charisma, padding, rowY + 84);
            DrawStatRow(ui, rect, 4, _localization.GetString("ui.field.education")!, _education, padding, rowY + 112);
            DrawStatRow(ui, rect, 5, _localization.GetString("ui.field.resolve")!, _resolve, padding, rowY + 140);

            string[] descriptions =
            [
                _localization.GetString("ui.field.strength.desc")!,
                _localization.GetString("ui.field.precision.desc")!,
                _localization.GetString("ui.field.awareness.desc")!,
                _localization.GetString("ui.field.charisma.desc")!,
                _localization.GetString("ui.field.education.desc")!,
                _localization.GetString("ui.field.resolve.desc")!,
            ];
            string[] names =
            [
                _localization.GetString("ui.field.strength")!,
                _localization.GetString("ui.field.precision")!,
                _localization.GetString("ui.field.awareness")!,
                _localization.GetString("ui.field.charisma")!,
                _localization.GetString("ui.field.education")!,
                _localization.GetString("ui.field.resolve")!,
            ];

            int infoY = Math.Max(292, rect.Size.Y - 112);
            int infoHeight = Math.Max(0, Math.Min(96, rect.Size.Y - infoY - 8));
            if (infoHeight >= 64)
            {
                ui.InfoBox(
                    Widgets.Rect(padding, infoY, rect.Size.X - padding * 2, infoHeight),
                    _localization.GetString("ui.menu.info")!,
                    descriptions[_focusedStat],
                    names[_focusedStat] + ":"
                );
            }
        }
    }

    private void DrawStatRow(
        UIBuilder<Material> ui,
        IntRect rect,
        int index,
        string label,
        int value,
        int padding,
        int y
    )
    {
        int rowWidth = rect.Size.X - padding * 2;
        using (ui.Element($"StatRow_{index}"))
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Constraints = Widgets.RectConstraints(Widgets.Rect(padding - 6, y - 3, rowWidth + 12, 28));
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(rowWidth + 12),
                Height = new Fixed(28),
            };

            bool focused = _focusedStat == index;
            bool hovering = ui.Hovering();
            if (hovering)
            {
                _focusedStat = index;
                focused = true;
            }

            ui.StatLabel(
                label,
                Widgets.Rect(6, 3, rowWidth - 112, 18),
                UiMetrics.MenuBodyFont,
                focused
            );
            ui.Label(
                value.ToString(),
                Widgets.Rect(rowWidth - 98, 3, 28, 18),
                UiMetrics.MenuBodyFont,
                UiTheme.Text,
                Anchors.Right | Anchors.Center
            );

            bool canDecrease = value > MIN_STAT;
            if (ui.ActionButton(
                    $"StatMinus_{index}",
                    Widgets.Rect(rowWidth - 62, 1, 24, 24),
                    "-",
                    ButtonVariant.Secondary,
                    canDecrease
                ))
            {
                _focusedStat = index;
                SetStat(index, value - 1);
                Widgets.Interactions.Click.WithButtonDecreaseAudio(_soundEffectService);
            }

            bool canIncrease = _spacerPoints > 0 && value < MAX_STAT;
            if (ui.ActionButton(
                    $"StatPlus_{index}",
                    Widgets.Rect(rowWidth - 30, 1, 24, 24),
                    "+",
                    ButtonVariant.Secondary,
                    canIncrease
                ))
            {
                _focusedStat = index;
                SetStat(index, value + 1);
                Widgets.Interactions.Click.WithButtonIncreaseAudio(_soundEffectService);
            }
        }
    }

    private void SetStat(int index, int value)
    {
        int oldValue = index switch
        {
            0 => _strength,
            1 => _precision,
            2 => _awareness,
            3 => _charisma,
            4 => _education,
            5 => _resolve,
            _ => 1,
        };
        int clamped = Math.Clamp(value, MIN_STAT, MAX_STAT);
        int change = clamped - oldValue;
        if (change == 0)
        {
            return;
        }

        if (change > 0 && _spacerPoints - change < 0)
        {
            return;
        }

        switch (index)
        {
            case 0: _strength = clamped; break;
            case 1: _precision = clamped; break;
            case 2: _awareness = clamped; break;
            case 3: _charisma = clamped; break;
            case 4: _education = clamped; break;
            case 5: _resolve = clamped; break;
        }

        _spacerPoints -= change;
    }

    private void TryCreate(Menu<MenuPage> menu)
    {
        string nameValue = CharacterNames.Clamp(_nameTextBox.Text.ToString().Trim(_characterNameTrimChars));
        if (string.IsNullOrWhiteSpace(nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetString("ui.notification.name.required")!));
            return;
        }

        if (_characterStorage.GetAllCharacters().Any(character => character.Name == nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetString("ui.notification.name.taken")!));
            return;
        }

        var character = new Character(
            WaywardBeyond.Version,
            Uuid.NewUuid().ToValue(),
            _Playtime: new Playtime(),
            nameValue,
            _strength,
            _precision,
            _awareness,
            _charisma,
            _education,
            _resolve,
            _Body: _characterAssetService.GetBodyId(_characterMaterialIndex),
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Creative,
            _Statistics: null,
            _Inventory: null
        );

        Task.Run(() =>
            {
                Result saveResult = _characterStorage.SaveCharacter(character);
                if (saveResult)
                {
                    _characterSaveManager.ActiveSave = character;
                    menu.GoToPage(MenuPage.SelectCharacter);
                    ResetState();
                }
                else
                {
                    _notificationService.Push(new Notification("Failed to create character!"));
                    _logger.LogError(saveResult.Exception, "Failed to save character \"{name}\" ({id}): {message}", character.Name, character.Uuid, saveResult.Message);
                }
            }
        );
    }

    private void CycleBody(int direction)
    {
        int count = _characterAssetService.GetAppearancesCount();
        if (count == 0)
        {
            return;
        }

        _characterMaterialIndex = MathS.WrapInt(_characterMaterialIndex + direction, 0, count - 1);
    }

    private void RandomizeName()
    {
        NameGenerator.Options options = new(
            TitleChance: 0.2f,
            FirstNameChance: 0.7f,
            LastNameChance: 1.0f,
            SubtitleChance: 0.1f,
            NicknameChance: 0.1f
        );

        string generatedName = _nameGenerator.Generate(
            key: _characterAssetService.GetBodyId(_characterMaterialIndex),
            options
        );
        _nameTextBox.Text.Clear();
        _nameTextBox.Text.Append(generatedName);
    }

    private void RandomizePoints()
    {
        if (_spacerPoints == 0)
        {
            ResetAttributes();
        }

        while (_spacerPoints > 0)
        {
            int[] candidates = Enumerable.Range(0, 6)
                .Where(index => index switch
                {
                    0 => _strength,
                    1 => _precision,
                    2 => _awareness,
                    3 => _charisma,
                    4 => _education,
                    5 => _resolve,
                    _ => MAX_STAT,
                } < MAX_STAT)
                .ToArray();

            if (candidates.Length == 0)
            {
                break;
            }

            int attributeIndex = candidates[_randomizer.NextInt(0, candidates.Length)];
            int value = attributeIndex switch
            {
                0 => _strength,
                1 => _precision,
                2 => _awareness,
                3 => _charisma,
                4 => _education,
                5 => _resolve,
                _ => MAX_STAT,
            };
            SetStat(attributeIndex, value + 1);
        }
    }

    private void ResetState()
    {
        _characterMaterialIndex = 0;
        _nameTextBox.Text.Clear();
        ResetAttributes();
    }

    private void ResetAttributes()
    {
        _spacerPoints = DEFAULT_SPACER_POINTS;
        _strength = 1;
        _precision = 1;
        _awareness = 1;
        _charisma = 1;
        _education = 1;
        _resolve = 1;
        _focusedStat = 0;
    }

    private static string FormatBodyId(string id)
    {
        string[] parts = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
