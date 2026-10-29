using Microsoft.Extensions.Logging;
using Reef;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System;
using System.IO;
using System.Threading.Tasks;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.IO;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Collects feedback with an optional screenshot and log history.</summary>
internal sealed class FeedbackModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.feedback");

    private readonly ILogger<FeedbackModal> _logger;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly FeedbackWebhook _feedbackWebhook;
    private readonly IRenderer _renderer;
    private readonly LogListener _logListener;
    private readonly NotificationService _notificationService;
    private readonly FeedbackView _view;
    private readonly FeedbackState _state;
    private Menu<Modal>? _menu;
    private double _time;
    private double _lastSubmissionTime = double.MinValue;

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
        _soundEffectService = soundEffectService;
        _localization = localization;
        _feedbackWebhook = feedbackWebhook;
        _renderer = renderer;
        _logListener = logListener;
        _notificationService = notificationService;
        _state = CreateState();
        _view = new FeedbackView(localization, inputService)
        {
            BackRequested = Back,
            SubmitRequested = Submit
        };
    }

    public Modal ID => Modal;

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        _menu = menu;
        _time = ui.Time;
        _view.Render(ui, _state);
        return Result.FromSuccess();
    }

    private void Back()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _menu?.GoBack();
    }

    private void Submit()
    {
        if (!_state.CanSubmit)
        {
            return;
        }

        double timeSinceLastSubmission = _time - _lastSubmissionTime;
        if (timeSinceLastSubmission <= 15)
        {
            var notification = new Notification(
                _localization.GetRequired("ui.message.feedback.tooSoon"),
                NotificationType.Interaction
            );
            _notificationService.Push(notification);
            return;
        }

        _lastSubmissionTime = _time;
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        var contact = _state.Contact.Text.ToString();
        var description = _state.Description.Text.ToString();
        _menu?.GoBack();
        Task.Run(SubmitAsync);

        async Task SubmitAsync()
        {
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
                _notificationService.Push(new Notification(_localization.GetRequired("ui.message.feedback.sent")));
            }
            else
            {
                _logger.LogError(result.Exception, result.Message);
                _notificationService.Push(new Notification(_localization.GetRequired("ui.message.feedback.failed")));
            }
        }
    }

    private static FeedbackState CreateState()
    {
        var contact = new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                MaxCharacters: 30,
                DisallowedCharacters: ['\t']
            )
        );
        var description = new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                SubmitBehavior: TextBoxState.SubmitBehavior.NewLine,
                MaxCharacters: 500
            )
        );
        return new FeedbackState(contact, description);
    }
}
