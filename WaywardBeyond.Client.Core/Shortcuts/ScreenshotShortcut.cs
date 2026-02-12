using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.UI;
using WaywardBeyond.Client.Core.UI.Layers;

namespace WaywardBeyond.Client.Core.Shortcuts;

internal class ScreenshotShortcut(in IRenderer renderer, in NotificationService notificationService) : ShortcutRegistration
{
    private readonly IRenderer _renderer = renderer;
    private readonly NotificationService _notificationService = notificationService;

    protected override string Name => "Take screenshot";
    protected override string Category => "General";
    protected override ShortcutModifiers Modifiers => ShortcutModifiers.None;
    protected override Key Key => Key.F12;
    
    protected override bool IsEnabled() => true;

    protected override void Action()
    {
        Texture screenshotTexture = _renderer.Screenshot();

        var fileName = $"{screenshotTexture.Name}.png";
        
        Directory.CreateDirectory("screenshots/");
        
        using (var screenshotStream = File.Create($"screenshots/{fileName}"))
        using (Image<Rgb24> image = Image.LoadPixelData<Rgb24>(screenshotTexture.Pixels, screenshotTexture.Width, screenshotTexture.Height))
        {
            image.SaveAsPng(screenshotStream);
        }

        _notificationService.Push(new Notification($"Saved screenshot \"{fileName}\""));
    }
}