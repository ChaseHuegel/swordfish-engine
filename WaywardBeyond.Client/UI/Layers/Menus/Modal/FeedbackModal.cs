using Microsoft.Extensions.Logging;
using Reef.Constraints;
using Reef.UI;
using Reef;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System.IO;
using System.Threading.Tasks;
using System;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.IO;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

internal class FeedbackModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.feedback");

    private readonly ILogger<FeedbackModal> _logger;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly FeedbackWebhook _feedbackWebhook;
    private readonly IRenderer _renderer;
    private readonly LogListener _logListener;
    private readonly NotificationService _notificationService;

    private readonly Widgets.ActionButtonOptions _submitButton;
    private readonly Widgets.ActionButtonOptions _backButton;
    private readonly FontOptions _fieldFontOptions;

    private TextBoxState _contactTextBox;
    private TextBoxState _descriptionTextBox;
    private double _lastSubmissionTime = double.MinValue;

    public Modal ID => Modal;

    public FeedbackModal(
        in ILogger<FeedbackModal> logger,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in FeedbackWebhook feedbackWebhook,
        in IRenderer renderer,
        in LogListener logListener,
        in NotificationService notificationService
    )
    {
        _logger = logger;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _feedbackWebhook = feedbackWebhook;
        _renderer = renderer;
        _logListener = logListener;
        _notificationService = notificationService;

        _submitButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);
        _backButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
        _fieldFontOptions = new FontOptions
        {
            Size = UiMetrics.MENU_BODY_FONT,
        };

        var contactTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.feedback.contactValue"),
            MaxCharacters: 30,
            DisallowedCharacters: ['\t'],
            Constraints: new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            }
        );
        _contactTextBox = new TextBoxState(initialValue: string.Empty, options: contactTextBoxOptions);

        var descriptionTextBoxOptions = new TextBoxState.Options(
            SubmitBehavior: TextBoxState.SubmitBehavior.NewLine,
            Placeholder: localization.GetString("ui.field.feedback.description"),
            MaxCharacters: 500,
            Constraints: new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f),
            }
        );

        _descriptionTextBox = new TextBoxState(initialValue: string.Empty, descriptionTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        int panelWidth = Math.Clamp(ui.Width - 160, 520, 860);
        int panelHeight = Math.Clamp(ui.Height - 160, 460, 620);

        ui.ModalFrame(
            panelWidth,
            panelHeight,
            UiTheme.ScrimHeavy,
            frame => DrawBody(frame, menu, panelWidth, panelHeight)
        );

        return Result.FromSuccess();
    }

    /// <summary>Draws the feedback fields, helper text, and actions inside the modal panel.</summary>
    private void DrawBody(UIBuilder<Material> ui, Menu<Modal> menu, int panelWidth, int panelHeight)
    {
        int padding = UiMetrics.MODAL_PADDING;
        int contentWidth = panelWidth - padding * 2;
        int descriptionHeight = Math.Clamp(panelHeight - 300, 140, 320);

        ui.Label(
            _localization.GetRequired("ui.menu.feedback"),
            Widgets.Rect(padding, 22, contentWidth, 30),
            UiMetrics.MENU_TITLE_FONT,
            UiTheme.Text,
            Anchors.Center
        );
        ui.Divider(
            Widgets.Rect(padding, 62, contentWidth, 1),
            UiTheme.Border
        );

        ui.Label(
            _localization.GetRequired("ui.feedback.message"),
            Widgets.Rect(padding, 78, contentWidth, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        using (ui.Element())
        {
            ui.Constraints = Widgets.RectConstraints(
                Widgets.Rect(padding, 102, contentWidth, descriptionHeight)
            );
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(contentWidth),
                Height = new Fixed(descriptionHeight),
            };
            ui.TextBox(
                id: "TextBox_Message",
                state: ref _descriptionTextBox,
                _fieldFontOptions,
                _inputService,
                _soundEffectService
            );
        }

        int contactY = 102 + descriptionHeight + 18;
        ui.Label(
            _localization.GetRequired("ui.feedback.contact"),
            Widgets.Rect(padding, contactY, contentWidth, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        using (ui.Element())
        {
            ui.Constraints = Widgets.RectConstraints(
                Widgets.Rect(padding, contactY + 20, contentWidth, UiMetrics.CONTROL_HEIGHT)
            );
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(contentWidth),
                Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
            };
            ui.TextBox(
                id: "TextBox_Contact",
                state: ref _contactTextBox,
                _fieldFontOptions,
                _inputService,
                _soundEffectService
            );
        }

        ui.Paragraph(
            _localization.GetRequired("ui.feedback.helper"),
            Widgets.Rect(
                padding,
                contactY + 58,
                contentWidth,
                18
            ),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.TextMuted
        );

        DrawFooter(ui, menu, padding, panelWidth, panelHeight);
    }

    /// <summary>Draws the back and submit actions along the panel footer.</summary>
    private void DrawFooter(UIBuilder<Material> ui, Menu<Modal> menu, int padding, int panelWidth, int panelHeight)
    {
        int buttonY = panelHeight - 60;
        Widgets.ActionButtonOptions backOptions = _backButton with
        {
            Rect = Widgets.Rect(padding, buttonY, 160, UiMetrics.ICON_BUTTON_SIZE),
        };

        if (ui.ActionButton("Button_Back", _localization.GetRequired("ui.button.back"), backOptions))
        {
            menu.GoBack();
        }

        bool canSubmit = _descriptionTextBox.Text.Length > 0;
        Widgets.ActionButtonOptions submitOptions = _submitButton with
        {
            Rect = Widgets.Rect(panelWidth - padding - 200, buttonY, 200, UiMetrics.ICON_BUTTON_SIZE),
            Enabled = canSubmit,
        };

        if (ui.ActionButton("Button_Submit", _localization.GetRequired("ui.button.submit"), submitOptions))
        {
            Submit(menu, ui.Time);
        }
    }

    private void Submit(Menu<Modal> menu, double time)
    {
        double timeSinceLastSubmission = time - _lastSubmissionTime;
        if (timeSinceLastSubmission <= 15)
        {
            var notification = new Notification(
                _localization.GetRequired("ui.notification.tooSoon"),
                NotificationType.Interaction
            );
            _notificationService.Push(notification);
            return;
        }

        _lastSubmissionTime = time;
        var contact = _contactTextBox.Text.ToString();
        var description = _descriptionTextBox.Text.ToString();
        menu.GoBack();
        Task.Run(SubmitAsync);

        async Task SubmitAsync()
        {
            //  Let the modal close before the log snapshot is taken.
            await Task.Delay(100);

            LogEventArgs[] logHistory = _logListener.GetHistory();

            await using var logStream = new MemoryStream();
            await using var logStreamWriter = new StreamWriter(logStream);
            foreach (LogEventArgs logEntry in logHistory)
            {
                await logStreamWriter.WriteLineAsync($"{logEntry.LogLevel}: {logEntry.CategoryName}\n      {logEntry.Log}");
            }

            await logStreamWriter.FlushAsync();
            logStream.Position = 0;

            Texture screenshotTexture = _renderer.Screenshot();

            await using var screenshotStream = new MemoryStream();
            using Image<Rgb24> image = Image.LoadPixelData<Rgb24>(
                screenshotTexture.Pixels,
                screenshotTexture.Width,
                screenshotTexture.Height
            );
            await image.SaveAsPngAsync(screenshotStream);
            screenshotStream.Position = 0;

            await using var log = new NamedStream(Name: $"{screenshotTexture.Name}.log", logStream);
            await using var screenshot = new NamedStream(Name: $"{screenshotTexture.Name}.png", screenshotStream);

            Result<Guid> result = await _feedbackWebhook.SendAsync(description, contact, log, screenshot);
            if (result.Success)
            {
                _logger.LogInformation($"Feedback submitted ({result.Value}).");
                _notificationService.Push(new Notification("Feedback submitted. Thank you, spacer!"));
            }
            else
            {
                _logger.LogError(result.Exception, result.Message);
                _notificationService.Push(new Notification("Feedback submission failed, please try again."));
            }
        }
    }
}
