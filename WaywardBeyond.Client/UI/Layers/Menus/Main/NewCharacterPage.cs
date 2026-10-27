using Microsoft.Extensions.Logging;
using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private const int DEFAULT_SPACER_POINTS = 18;
    private const int MIN_STAT = 1;
    private const int MAX_STAT = 8;
    private const int PANEL_PADDING = 20;
    private const int INPUT_WIDTH = 300;
    private const int BODY_BOX_SIZE = 208;
    private const int BODY_BOX_Y = 96;
    private const int BODY_IMAGE_SIZE = 192;

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
                Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
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

            DrawPanes(ui, layout);
            DrawFooter(ui, layout, menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the identity and evaluation panes for the current width.</summary>
    private void DrawPanes(UIBuilder<Material> ui, in MenuLayout layout)
    {
        if (!layout.UseTabs)
        {
            DrawIdentity(ui, layout.Left);
            DrawEvaluation(ui, layout.Right);
            return;
        }

        DrawTabs(ui, layout);
        if (_narrowTab == 0)
        {
            DrawIdentity(ui, layout.Right);
            return;
        }

        DrawEvaluation(ui, layout.Right);
    }

    /// <summary>Draws the narrow-layout tabs.</summary>
    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;

        IntRect identityRect = Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool identitySelected = _narrowTab == 0;
        string identityLabel = _localization.GetRequired("ui.menu.colonistIdentity");
        if (ui.Tab("IdentityTab", identityRect, identityLabel, identitySelected))
        {
            _narrowTab = 0;
        }

        IntRect evaluationRect = Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool evaluationSelected = _narrowTab == 1;
        string evaluationLabel = _localization.GetRequired("ui.menu.spacerEvaluation");
        if (ui.Tab("EvaluationTab", evaluationRect, evaluationLabel, evaluationSelected))
        {
            _narrowTab = 1;
        }
    }

    /// <summary>Draws the page footer, then opens the created character or leaves the page.</summary>
    private void DrawFooter(UIBuilder<Material> ui, in MenuLayout layout, Menu<MenuPage> menu)
    {
        string currentName = CurrentName();
        bool nameMissing = string.IsNullOrWhiteSpace(currentName);
        bool nameTaken = !nameMissing
            && _characterStorage.GetAllCharacters().Any(character => character.Name == currentName);
        bool canCreate = !nameMissing && !nameTaken;
        FooterStatus status = ResolveFooterStatus(nameMissing, nameTaken);

        string backLabel = _localization.GetRequired("ui.button.back");
        if (ui.ActionButton("Button_Back", layout.ReturnButton, backLabel, _backButton))
        {
            menu.GoToPage(MenuPage.SelectCharacter);
        }

        string createLabel = _localization.GetRequired("ui.button.createCharacter");
        var createOptions = _createButton with { Enabled = canCreate };
        if (ui.ActionButton("Button_CreateCharacter", layout.PrimaryButton, createLabel, createOptions))
        {
            RequestCreate(menu);
        }

        ui.FooterStatus(
            layout.Footer,
            layout.ReturnButton,
            layout.PrimaryButton,
            status.Message,
            status.Color
        );
    }

    /// <summary>Gets the footer message and its color for the current name and unspent spacer points.</summary>
    private FooterStatus ResolveFooterStatus(bool nameMissing, bool nameTaken)
    {
        if (nameMissing)
        {
            return new FooterStatus(_localization.GetString("ui.notification.name.required"), UiTheme.Danger);
        }

        if (nameTaken)
        {
            return new FooterStatus(_localization.GetString("ui.notification.name.taken"), UiTheme.Danger);
        }

        if (_spacerPoints == 0)
        {
            return default;
        }

        string message = string.Format(
            _localization.GetRequired("ui.menu.unspentPointsWarning"),
            _spacerPoints
        );

        return new FooterStatus(message, UiTheme.Warning);
    }

    /// <summary>A footer message and the color to draw it in.</summary>
    private readonly record struct FooterStatus(string? Message, Vector4 Color);

    /// <summary>Creates the character, or asks for confirmation first while spacer points are unspent.</summary>
    private void RequestCreate(Menu<MenuPage> menu)
    {
        if (_spacerPoints == 0)
        {
            TryCreate(menu);
            return;
        }

        string title = _localization.GetRequired("ui.menu.unspentPoints");
        string body = string.Format(
            _localization.GetRequired("ui.menu.unspentPointsBody"),
            _spacerPoints
        );
        string createLabel = _localization.GetRequired("ui.button.createCharacter");
        string cancelLabel = _localization.GetRequired("ui.button.cancel");
        _modalMenu.GoToPage(_confirmModal.Create(() => TryCreate(menu), title, body, createLabel, cancelLabel));
    }

    /// <summary>Draws the identity panel.</summary>
    private void DrawIdentity(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.colonistIdentity")))
        {
            DrawNameField(ui, rect);
            DrawBodyPreview(ui, rect);
        }
    }

    /// <summary>Draws the character name field and its randomize button.</summary>
    private void DrawNameField(UIBuilder<Material> ui, IntRect rect)
    {
        int nameGroupWidth = INPUT_WIDTH + 8 + UiMetrics.SMALL_ICON_BUTTON_SIZE;
        int nameGroupX = (rect.Size.X - nameGroupWidth) / 2;

        ui.Label(
            _localization.GetRequired("ui.field.characterNameLabel"),
            Widgets.Rect(nameGroupX, 38, nameGroupWidth, 16),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );

        IntRect textBoxRect = Widgets.Rect(nameGroupX, 54, INPUT_WIDTH, UiMetrics.CONTROL_HEIGHT);
        Widgets.Interactions nameInteractions = ui.MenuTextBox(
            "TextBox_CharacterName",
            ref _nameTextBox,
            textBoxRect,
            new FontOptions { Size = UiMetrics.MENU_BODY_FONT },
            _inputService,
            _soundEffectService
        );

        IntRect randomRect = Widgets.Rect(
            nameGroupX + INPUT_WIDTH + 8,
            54,
            UiMetrics.SMALL_ICON_BUTTON_SIZE,
            UiMetrics.SMALL_ICON_BUTTON_SIZE
        );
        Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
        if (ui.IconButton("Button_RandomName", randomRect, MenuGlyphs.DICE, randomOptions))
        {
            RandomizeName();
        }
    }

    /// <summary>Draws the body preview box, its cycle buttons, and the preset labels.</summary>
    private void DrawBodyPreview(UIBuilder<Material> ui, IntRect rect)
    {
        int boxX = (rect.Size.X - BODY_BOX_SIZE) / 2;
        IntRect boxRect = Widgets.Rect(boxX, BODY_BOX_Y, BODY_BOX_SIZE, BODY_BOX_SIZE);

        DrawBodyImage(ui, boxRect);

        int buttonY = boxRect.Top + boxRect.Size.Y / 2 - 16;
        IntRect previousRect = Widgets.Rect(boxX - 40, buttonY, 32, 32);
        Widgets.IconButtonOptions previousOptions = new(SoundEffectService: _soundEffectService);
        if (ui.IconButton("Button_PreviousCharacter", previousRect, MenuGlyphs.CHEVRON_LEFT, previousOptions))
        {
            CycleBody(-1);
        }

        IntRect nextRect = Widgets.Rect(boxRect.Right + 8, buttonY, 32, 32);
        Widgets.IconButtonOptions nextOptions = new(SoundEffectService: _soundEffectService);
        if (ui.IconButton("Button_NextCharacter", nextRect, MenuGlyphs.CHEVRON_RIGHT, nextOptions))
        {
            CycleBody(1);
        }

        IntRect randomRect = Widgets.Rect(boxRect.Right + 48, buttonY, 32, 32);
        Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
        if (ui.IconButton("Button_RandomCharacter", randomRect, MenuGlyphs.DICE, randomOptions)
            && _characterAssetService.GetAppearancesCount() > 0)
        {
            _characterMaterialIndex = _randomizer.NextInt(0, _characterAssetService.GetAppearancesCount());
        }

        ui.Label(
            _localization.GetRequired("ui.field.bodyPreset"),
            Widgets.Rect(PANEL_PADDING, boxRect.Bottom + 14, rect.Size.X - PANEL_PADDING * 2, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Center
        );
        ui.Label(
            FormatBodyId(_characterAssetService.GetBodyId(_characterMaterialIndex)),
            Widgets.Rect(PANEL_PADDING, boxRect.Bottom + 34, rect.Size.X - PANEL_PADDING * 2, 24),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.AccentBright,
            Anchors.Center
        );
    }

    /// <summary>Draws the body preview frame and the current appearance inside it.</summary>
    private void DrawBodyImage(UIBuilder<Material> ui, IntRect box)
    {
        using (ui.Element())
        {
            ui.Color = UiTheme.PanelInner;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Border,
            };
            ui.Constraints = Widgets.RectConstraints(box);
        }

        Material appearance = _characterAssetService.GetAppearanceMaterial(
            _characterAssetService.GetBodyId(_characterMaterialIndex)
        );
        using (ui.Image(appearance))
        {
            ui.Constraints = new Constraints
            {
                X = new Fixed(box.Left + (box.Size.X - BODY_IMAGE_SIZE) / 2),
                Y = new Fixed(box.Top + 8),
                Width = new Fixed(BODY_IMAGE_SIZE),
                Height = new Fixed(BODY_IMAGE_SIZE),
            };
        }
    }

    /// <summary>Draws the spacer evaluation panel.</summary>
    private void DrawEvaluation(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.spacerEvaluation")))
        {
            int padding = 16;

            ui.Paragraph(
                _localization.GetRequired("ui.field.spacer.description"),
                Widgets.Rect(padding, 38, rect.Size.X - padding * 2, 60),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextMuted
            );

            ui.Label(
                _localization.GetRequired("ui.field.spacer.points"),
                Widgets.Rect(padding, 104, rect.Size.X - padding * 2 - 48, 20),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                _spacerPoints.ToString(),
                Widgets.Rect(rect.Size.X - padding - 76, 104, 36, 20),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Primary,
                Anchors.Right | Anchors.Center
            );

            IntRect randomRect = Widgets.Rect(rect.Size.X - padding - 32, 98, 32, 32);
            Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
            if (ui.IconButton("Button_RandomPoints", randomRect, MenuGlyphs.DICE, randomOptions))
            {
                RandomizePoints();
            }

            DrawStatRows(ui, rect, padding);
            DrawFocusedStatInfo(ui, rect, padding);
        }
    }

    /// <summary>Draws the six spacer stat rows.</summary>
    private void DrawStatRows(UIBuilder<Material> ui, IntRect rect, int padding)
    {
        const int rowY = 132;

        DrawStatRow(ui, rect, 0, _localization.GetRequired("ui.field.strength"), _strength, padding, rowY);
        DrawStatRow(ui, rect, 1, _localization.GetRequired("ui.field.precision"), _precision, padding, rowY + 28);
        DrawStatRow(ui, rect, 2, _localization.GetRequired("ui.field.awareness"), _awareness, padding, rowY + 56);
        DrawStatRow(ui, rect, 3, _localization.GetRequired("ui.field.charisma"), _charisma, padding, rowY + 84);
        DrawStatRow(ui, rect, 4, _localization.GetRequired("ui.field.education"), _education, padding, rowY + 112);
        DrawStatRow(ui, rect, 5, _localization.GetRequired("ui.field.resolve"), _resolve, padding, rowY + 140);
    }

    /// <summary>Draws the description box for the focused stat when the panel is tall enough.</summary>
    private void DrawFocusedStatInfo(UIBuilder<Material> ui, IntRect rect, int padding)
    {
        string[] descriptions =
        [
            _localization.GetRequired("ui.field.strength.desc"),
            _localization.GetRequired("ui.field.precision.desc"),
            _localization.GetRequired("ui.field.awareness.desc"),
            _localization.GetRequired("ui.field.charisma.desc"),
            _localization.GetRequired("ui.field.education.desc"),
            _localization.GetRequired("ui.field.resolve.desc"),
        ];
        string[] names =
        [
            _localization.GetRequired("ui.field.strength"),
            _localization.GetRequired("ui.field.precision"),
            _localization.GetRequired("ui.field.awareness"),
            _localization.GetRequired("ui.field.charisma"),
            _localization.GetRequired("ui.field.education"),
            _localization.GetRequired("ui.field.resolve"),
        ];

        int infoY = Math.Max(292, rect.Size.Y - 112);
        int infoHeight = Math.Max(0, Math.Min(96, rect.Size.Y - infoY - 8));
        if (infoHeight < 64)
        {
            return;
        }

        IntRect infoRect = Widgets.Rect(padding, infoY, rect.Size.X - padding * 2, infoHeight);
        ui.InfoBox(
            infoRect,
            _localization.GetRequired("ui.menu.info"),
            descriptions[_focusedStat],
            names[_focusedStat] + ":"
        );
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
                UiMetrics.MENU_BODY_FONT,
                focused
            );
            ui.Label(
                value.ToString(),
                Widgets.Rect(rowWidth - 98, 3, 28, 18),
                UiMetrics.MENU_BODY_FONT,
                UiTheme.Text,
                Anchors.Right | Anchors.Center
            );

            bool canDecrease = value > MIN_STAT;
            IntRect minusRect = Widgets.Rect(rowWidth - 62, 1, 24, 24);
            if (ui.ActionButton($"StatMinus_{index}", minusRect, "-", ButtonVariant.Secondary, canDecrease))
            {
                _focusedStat = index;
                SetStat(index, value - 1);
                Widgets.Interactions.Click.WithButtonAudio(_soundEffectService, Widgets.InterfaceSound.Decrease);
            }

            bool canIncrease = _spacerPoints > 0 && value < MAX_STAT;
            IntRect plusRect = Widgets.Rect(rowWidth - 30, 1, 24, 24);
            if (ui.ActionButton($"StatPlus_{index}", plusRect, "+", ButtonVariant.Secondary, canIncrease))
            {
                _focusedStat = index;
                SetStat(index, value + 1);
                Widgets.Interactions.Click.WithButtonAudio(_soundEffectService, Widgets.InterfaceSound.Increase);
            }
        }
    }

    /// <summary>Gets the trimmed character name the field currently holds.</summary>
    private string CurrentName()
    {
        return CharacterNames.Clamp(_nameTextBox.Text.ToString().Trim(_characterNameTrimChars));
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
            case 0:
                _strength = clamped;
                break;
            case 1:
                _precision = clamped;
                break;
            case 2:
                _awareness = clamped;
                break;
            case 3:
                _charisma = clamped;
                break;
            case 4:
                _education = clamped;
                break;
            case 5:
                _resolve = clamped;
                break;
        }

        _spacerPoints -= change;
    }

    private void TryCreate(Menu<MenuPage> menu)
    {
        string nameValue = CurrentName();
        if (string.IsNullOrWhiteSpace(nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetRequired("ui.notification.name.required")));
            return;
        }

        if (_characterStorage.GetAllCharacters().Any(character => character.Name == nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetRequired("ui.notification.name.taken")));
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

        Task.Run(() => SaveCharacter(character, menu));
    }

    /// <summary>Saves the new character, then opens the character list.</summary>
    private void SaveCharacter(Character character, Menu<MenuPage> menu)
    {
        Result saveResult = _characterStorage.SaveCharacter(character);
        if (saveResult)
        {
            _characterSaveManager.ActiveSave = character;
            menu.GoToPage(MenuPage.SelectCharacter);
            ResetState();
            return;
        }

        _notificationService.Push(new Notification("Failed to create character!"));
        _logger.LogError(
            saveResult.Exception,
            "Failed to save character \"{name}\" ({id}): {message}",
            character.Name,
            character.Uuid,
            saveResult.Message
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
