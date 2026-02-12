using Microsoft.Extensions.Logging;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class MainMenuFeedbackPage(
    in ILogger<FeedbackPage<MenuPage>> logger,
    in IInputService inputService,
    in IAudioService audioService,
    in VolumeSettings volumeSettings,
    in ILocalization localization,
    in FeedbackWebhook feedbackWebhook,
    in IRenderer renderer
) : FeedbackPage<MenuPage>(
    in logger,
    in inputService,
    in audioService,
    in volumeSettings,
    in localization,
    in feedbackWebhook,
    in renderer
) {
    public override MenuPage ID => MenuPage.Feedback;
}