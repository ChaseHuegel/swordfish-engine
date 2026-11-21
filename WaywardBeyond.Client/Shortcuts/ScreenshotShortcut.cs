using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.UI;

namespace WaywardBeyond.Client.Shortcuts;

/// <summary>Captures a screenshot on F12 and saves it under the configured data directory.</summary>
internal sealed class ScreenshotShortcut(
    in IRenderer renderer,
    in NotificationService notificationService,
    in LocalizedFormatter localizedFormatter,
    in AppPaths appPaths
) : ShortcutRegistration
{
    private const string SCREENSHOTS_DIRECTORY = "screenshots";

    private readonly IRenderer _renderer = renderer;
    private readonly NotificationService _notificationService = notificationService;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;
    private readonly AppPaths _appPaths = appPaths;
    private readonly Lock _screenshotFileNameLock = new();

    /// <inheritdoc/>
    protected override string Name => "Take screenshot";

    /// <inheritdoc/>
    protected override string Category => "General";

    /// <inheritdoc/>
    protected override ShortcutModifiers Modifiers => ShortcutModifiers.None;

    /// <inheritdoc/>
    protected override Key Key => Key.F12;

    /// <inheritdoc/>
    protected override bool IsEnabled() => true;

    /// <inheritdoc/>
    protected override void Action()
    {
        Task.Run(SaveScreenshotAsync);
    }

    /// <summary>Captures the current frame and writes it as a PNG under the screenshots directory.</summary>
    internal async Task SaveScreenshotAsync()
    {
        Texture screenshotTexture = _renderer.Screenshot();

        PathInfo screenshotsDirectory = _appPaths.DataRoot.At(SCREENSHOTS_DIRECTORY);
        screenshotsDirectory.CreateDirectory();

        PathInfo path;
        await using (FileStream fileStream = CreateUniqueFile(screenshotsDirectory, screenshotTexture.Name, out path))
        {
            using Image<Rgb24> image = Image.LoadPixelData<Rgb24>(screenshotTexture.Pixels, screenshotTexture.Width, screenshotTexture.Height);
            await image.SaveAsPngAsync(fileStream);
        }

        _notificationService.Push(new Notification(
            _localizedFormatter.GetString("notification.screenshot.saved", path.Value)
        ));
    }

    private FileStream CreateUniqueFile(PathInfo directory, string baseName, out PathInfo path)
    {
        lock (_screenshotFileNameLock)
        {
            path = directory.At($"{baseName}.png");
            var duplicates = 0;
            while (File.Exists(path.Value))
            {
                path = directory.At($"{baseName}_{++duplicates}.png");
            }

            return File.Create(path.Value);
        }
    }
}