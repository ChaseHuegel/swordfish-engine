using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(WaywardBeyond.AssetStudio.Tests.TestAppBuilder))]

namespace WaywardBeyond.AssetStudio.Tests;

/// <summary>
///     Configures the Avalonia headless test application with Skia rendering and Inter fonts.
/// </summary>
public sealed class TestAppBuilder
{
    /// <summary>
    ///     Creates an Avalonia app builder configured for Skia-backed headless rendering tests.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
    }
}
