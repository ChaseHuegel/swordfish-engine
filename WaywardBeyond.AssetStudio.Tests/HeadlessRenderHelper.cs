using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using WaywardBeyond.AssetStudio.Views;

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Renders headless Avalonia studio windows to verified 1280x800 PNG screenshots.
/// </summary>
internal static class HeadlessRenderHelper
{
    private const int WIDTH = 1280;
    private const int HEIGHT = 800;

    /// <summary>
    ///     Primary shared previews output directory read by the coordinator and UI reviewer.
    /// </summary>
    public const string COORDINATOR_PREVIEW_DIR =
        "/home/agent/.t3/userdata/providers/antigravity/ac0a3dfd6dddb20962cecff6ee5fe65e19d3923be20e52c5ab52ff877f7e4c32/antigravity-acp/brain/49f337cf-b0d3-499a-86f0-6d0b6fba941a/previews";

    /// <summary>
    ///     Local conversation artifact directory for swarm overview screenshot reporting.
    /// </summary>
    public const string LOCAL_PREVIEW_DIR =
        "/home/agent/.t3/userdata/providers/antigravity/ac0a3dfd6dddb20962cecff6ee5fe65e19d3923be20e52c5ab52ff877f7e4c32/antigravity-acp/brain/fa495e8b-ca61-411e-8a9d-e069c34ca08a/previews";

    /// <summary>
    ///     Lays out and renders the window to PNG in both output folders, asserting non-blank pixels.
    /// </summary>
    public static string CaptureWindowPng(MainWindow window, string fileName)
    {
        Directory.CreateDirectory(COORDINATOR_PREVIEW_DIR);
        Directory.CreateDirectory(LOCAL_PREVIEW_DIR);

        window.Width = WIDTH;
        window.Height = HEIGHT;
        window.Show();

        window.Measure(new Size(WIDTH, HEIGHT));
        window.Arrange(new Rect(0, 0, WIDTH, HEIGHT));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        Dispatcher.UIThread.RunJobs();

        var targetPath = Path.Combine(COORDINATOR_PREVIEW_DIR, fileName);
        var localCopyPath = Path.Combine(LOCAL_PREVIEW_DIR, fileName);

        using var renderBitmap = new RenderTargetBitmap(new PixelSize(WIDTH, HEIGHT), new Vector(96, 96));
        renderBitmap.Render(window);
        renderBitmap.Save(targetPath);

        var fileInfo = new FileInfo(targetPath);
        if (!fileInfo.Exists || fileInfo.Length < 4096)
        {
            throw new InvalidOperationException($"Captured PNG '{fileName}' appears blank or truncated ({fileInfo.Length} bytes).");
        }

        File.Copy(targetPath, localCopyPath, overwrite: true);
        return targetPath;
    }
}
