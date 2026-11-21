#nullable enable
using System.IO;
using System.Threading.Tasks;
using DryIoc;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SmartFormat;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.UI;
using Xunit;

namespace Swordfish.Tests;

public class ScreenshotShortcutTests
{
    private sealed class FakeRenderer(string screenshotName) : IRenderer
    {
        public DataBinding<int> DrawCalls { get; } = new(0);

        public Texture Screenshot()
        {
            byte[] pixels = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255];
            return new Texture(screenshotName, pixels, width: 2, height: 2, mipmaps: false);
        }
    }

    [Fact]
    public async Task SaveScreenshot_WritesPngUnderConfiguredDataDirectoryAndNotifies()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(tempRoot));
            var windowContext = new FakeWindowContext();
            using var notifications = new NotificationService(NullLogger<NotificationService>.Instance, windowContext);
            var formatter = new LocalizedFormatter(new TestLocalization(), Smart.CreateDefaultSmartFormat());
            var renderer = new FakeRenderer("26-11-21_12.00.00");

            using var container = new Container();
            container.RegisterInstance<IRenderer>(renderer);
            container.RegisterInstance(notifications);
            container.RegisterInstance(formatter);
            container.RegisterInstance(appPaths);
            container.Register<ScreenshotShortcut>();

            ScreenshotShortcut shortcut = container.Resolve<ScreenshotShortcut>();
            await shortcut.SaveScreenshotAsync();

            string expectedPath = Path.Combine(tempRoot, "screenshots", "26-11-21_12.00.00.png");
            Assert.True(File.Exists(expectedPath));

            ImageInfo info = Image.Identify(expectedPath);
            Assert.Equal(2, info.Width);
            Assert.Equal(2, info.Height);

            windowContext.Update?.Invoke(0.016);
            Assert.Single(notifications.GetActiveNotifications());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveScreenshot_AppendsDuplicateSuffixInDataDirectory()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(tempRoot));
            var windowContext = new FakeWindowContext();
            using var notifications = new NotificationService(NullLogger<NotificationService>.Instance, windowContext);
            var formatter = new LocalizedFormatter(new TestLocalization(), Smart.CreateDefaultSmartFormat());
            var renderer = new FakeRenderer("26-11-21_12.00.00");
            var shortcut = new ScreenshotShortcut(renderer, notifications, formatter, appPaths);

            await shortcut.SaveScreenshotAsync();
            await shortcut.SaveScreenshotAsync();

            string firstPath = Path.Combine(tempRoot, "screenshots", "26-11-21_12.00.00.png");
            string secondPath = Path.Combine(tempRoot, "screenshots", "26-11-21_12.00.00_1.png");
            Assert.True(File.Exists(firstPath));
            Assert.True(File.Exists(secondPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
