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
    private const string PREVIEW_OUT_DIRS_ENV = "WB_STUDIO_PREVIEW_OUT_DIRS";

    /// <summary>
    ///     Gets the first preview output folder.
    /// </summary>
    public static string PrimaryPreviewDirectory => ResolvePreviewOutputDirs()[0];

    /// <summary>
    ///     Lays out and renders the window to PNG in each preview output folder, asserting non-blank pixels.
    /// </summary>
    /// <remarks>
    ///     Set <c>WB_STUDIO_PREVIEW_OUT_DIRS</c> to a <see cref="Path.PathSeparator"/>-separated list to
    ///     write additional copies. The default is a temporary folder.
    /// </remarks>
    public static string CaptureWindowPng(MainWindow window, string fileName)
    {
        string[] outputDirs = ResolvePreviewOutputDirs();

        window.Width = WIDTH;
        window.Height = HEIGHT;
        window.Show();

        window.Measure(new Size(WIDTH, HEIGHT));
        window.Arrange(new Rect(0, 0, WIDTH, HEIGHT));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        Dispatcher.UIThread.RunJobs();

        using var renderBitmap = new RenderTargetBitmap(new PixelSize(WIDTH, HEIGHT), new Vector(96, 96));
        renderBitmap.Render(window);

        string? targetPath = null;
        for (var i = 0; i < outputDirs.Length; i++)
        {
            string outputDir = outputDirs[i];
            Directory.CreateDirectory(outputDir);
            string path = Path.Combine(outputDir, fileName);
            renderBitmap.Save(path);

            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length < 4096)
            {
                throw new InvalidOperationException($"Captured PNG '{fileName}' appears blank or truncated ({fileInfo.Length} bytes).");
            }

            targetPath ??= path;
        }

        return targetPath!;
    }

    private static string[] ResolvePreviewOutputDirs()
    {
        string? configured = Environment.GetEnvironmentVariable(PREVIEW_OUT_DIRS_ENV);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return [Path.Combine(Path.GetTempPath(), "wb-asset-studio-previews")];
        }

        string[] outputDirs = configured.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return outputDirs.Length > 0
            ? outputDirs
            : [Path.Combine(Path.GetTempPath(), "wb-asset-studio-previews")];
    }
}
