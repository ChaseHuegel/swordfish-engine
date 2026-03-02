using System.IO;
using System.Threading.Tasks;
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
    private readonly object _screenshotFileNameLock = new();

    protected override string Name => "Take screenshot";
    protected override string Category => "General";
    protected override ShortcutModifiers Modifiers => ShortcutModifiers.None;
    protected override Key Key => Key.F12;
    
    protected override bool IsEnabled() => true;

    protected override void Action()
    {
        Task.Run(ActionAsync);
    }

    private async Task ActionAsync()
    {
        Texture screenshotTexture = _renderer.Screenshot();
        
        Directory.CreateDirectory("screenshots/");

        //  Create the file, handling duplicates
        FileStream fileStream;
        var path = $"screenshots/{screenshotTexture.Name}.png";
        lock (_screenshotFileNameLock)
        {
            var duplicates = 0;
            while (File.Exists(path))
            {
                path = $"screenshots/{screenshotTexture.Name}_{++duplicates}.png";
            }

            fileStream = File.Create(path);
        }
        
        using (Image<Rgb24> image = Image.LoadPixelData<Rgb24>(screenshotTexture.Pixels, screenshotTexture.Width, screenshotTexture.Height))
        {
            await image.SaveAsPngAsync(fileStream);
        }
        
        await fileStream.DisposeAsync();

        _notificationService.Push(new Notification($"Saved screenshot \"{path}\""));
    }
}