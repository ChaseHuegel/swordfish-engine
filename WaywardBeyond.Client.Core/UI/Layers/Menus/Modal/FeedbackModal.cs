using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.Constraints;
using Reef.UI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.IO;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

internal class FeedbackModal : IMenuPage<Modal>
{
    public static Modal Modal { get; } = new(id: "core.feedback");
    
    private readonly ILogger<FeedbackModal> _logger;
    private readonly IInputService _inputService;
    private readonly IAudioService _audioService;
    private readonly VolumeSettings _volumeSettings;
    private readonly ILocalization _localization;
    private readonly FeedbackWebhook _feedbackWebhook;
    private readonly IRenderer _renderer;
    private readonly LogListener _logListener;
    private readonly NotificationService _notificationService;

    private readonly Widgets.ButtonOptions _menuButtonOptions;
    private readonly Widgets.ButtonOptions _buttonOptions;
    private readonly FontOptions _fieldFontOptions;

    private TextBoxState _contactTextBox;
    private TextBoxState _descriptionTextBox;
    private double _lastSubmissionTime = double.MinValue;

    public Modal ID => Modal;

    public FeedbackModal(
        in ILogger<FeedbackModal> logger,
        in IInputService inputService,
        in IAudioService audioService,
        in VolumeSettings volumeSettings,
        in ILocalization localization,
        in FeedbackWebhook feedbackWebhook,
        in IRenderer renderer,
        in LogListener logListener,
        in NotificationService notificationService
    ) {
        _logger = logger;
        _inputService = inputService;
        _audioService = audioService;
        _volumeSettings = volumeSettings;
        _localization = localization;
        _feedbackWebhook = feedbackWebhook;
        _renderer = renderer;
        _logListener = logListener;
        _notificationService = notificationService;

        _menuButtonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 32,
            },
            new Widgets.AudioOptions(audioService, volumeSettings)
        );
        
        _buttonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 24,
            },
            new Widgets.AudioOptions(audioService, volumeSettings)
        );
        
        _fieldFontOptions = new FontOptions
        {
            Size = 20,
        };
        
        var contactTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.feedback.contact"),
            MaxCharacters: 30,
            DisallowedCharacters: ['\t'],
            Constraints: new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(400),
            }
        );
        _contactTextBox = new TextBoxState(initialValue: string.Empty, options: contactTextBoxOptions);
        
        var descriptionTextBoxOptions = new TextBoxState.Options(
            SubmitBehavior: TextBoxState.SubmitBehavior.NewLine,
            Placeholder: localization.GetString("ui.field.feedback.description"),
            MaxCharacters: 500,
            Constraints: new Constraints
            {
                Width = new Fixed(600),
                Height = new Fixed(350),
            }
        );
        
        _descriptionTextBox = new TextBoxState(initialValue: string.Empty, descriptionTextBoxOptions);
    }
    
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<Modal> menu)
    {
        using (ui.Element())
        {
            //  Fullscreen tint
            ui.Color = new Vector4(0f, 0f, 0f, 0.95f);
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Padding = new Padding
            {
                Left = 20,
                Top = 20,
                Right = 20,
                Bottom = 20,
            };
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
            
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
                
                using (ui.Text(_localization.GetString("ui.menu.feedback")!))
                {
                    ui.FontSize = 24;
                }
            }
            
            using (ui.Element())
            {
                ui.Spacing = 8;
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                ui.TextBox(id: "TextBox_Message", state: ref _descriptionTextBox, _fieldFontOptions, _inputService, _audioService, _volumeSettings);
                ui.TextBox(id: "TextBox_Contact", state: ref _contactTextBox, _fieldFontOptions, _inputService, _audioService, _volumeSettings);
                
                using (ui.TextButton(id: "Button_Submit", text: _localization.GetString("ui.button.submit")!, _buttonOptions, out Widgets.Interactions interactions))
                {
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                    
                    if (interactions.Has(Widgets.Interactions.Click))
                    {
                        double timeSinceLastSubmission = ui.Time - _lastSubmissionTime;
                        bool canSubmit = timeSinceLastSubmission > 15;
                        if (!canSubmit)
                        {
                            var notification = new Notification(_localization.GetString("ui.notification.tooSoon")!, NotificationType.Interaction);
                            _notificationService.Push(notification);
                        }
                        else
                        {
                            _lastSubmissionTime = ui.Time;
                            var contact = _contactTextBox.Text.ToString();
                            var description = _descriptionTextBox.Text.ToString();
                            Task.Run(SubmitAsync);

                            async Task SubmitAsync()
                            {
                                //  Collect log info
                                LogEventArgs[] logHistory = _logListener.GetHistory();

                                //  Create the log stream
                                await using var logStream = new MemoryStream();
                                await using var logStreamWriter = new StreamWriter(logStream);
                                foreach (LogEventArgs logEntry in logHistory)
                                {
                                    await logStreamWriter.WriteLineAsync(
                                        $"{logEntry.LogLevel}: {logEntry.CategoryName}\n      {logEntry.Log}");
                                }

                                await logStreamWriter.FlushAsync();
                                logStream.Position = 0;

                                //  Collect a screenshot
                                Texture screenshotTexture = _renderer.Screenshot();

                                //  Create the screenshot stream
                                await using var screenshotStream = new MemoryStream();
                                using Image<Rgb24> image = Image.LoadPixelData<Rgb24>(screenshotTexture.Pixels,
                                    screenshotTexture.Width, screenshotTexture.Height);
                                await image.SaveAsPngAsync(screenshotStream);
                                screenshotStream.Position = 0;

                                //  Name the streams
                                await using var log = new NamedStream(Name: $"{screenshotTexture.Name}.log", logStream);
                                await using var screenshot = new NamedStream(Name: $"{screenshotTexture.Name}.png",
                                    screenshotStream);

                                //  Send the feedback form
                                Result result = await _feedbackWebhook.SendAsync(description, contact, log, screenshot);
                                if (result.Success)
                                {
                                    _logger.LogInformation("Feedback submitted successfully.");
                                }
                                else
                                {
                                    _logger.LogError(result.Exception, result.Message);
                                }
                            }
                        }
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
                
                using (ui.TextButton(id: "Button_Back", text: "Back", _menuButtonOptions, out Widgets.Interactions interactions))
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
    }
}