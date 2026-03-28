using System.Drawing;
using HardwareInformation;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Settings;
using WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry : IAutoActivate
{
    private readonly IWindowContext _windowContext;
    private readonly ModalMenu _modalMenu;

    public Entry(
        in ILogger<Entry> logger,
        in IWindowContext windowContext,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings,
        in IFileParseService fileParseService,
        in IShortcutService shortcutService,
        in ModalMenu modalMenu
    ) {
        _windowContext = windowContext;
        _modalMenu = modalMenu;

        windowSettings.Title.Set("Wayward Beyond");
        logger.LogInformation("Starting Wayward Beyond {version}", WaywardBeyond.Version.Name);

        MachineInformation? machineInformation = MachineInformationGatherer.GatherInformation();
        if (machineInformation == null)
        {
            return;
        }

        logger.LogInformation("OS: {os}", machineInformation.OperatingSystem.VersionString);
        logger.LogInformation("CPU: {cpu}", machineInformation.Cpu.Name); 
        logger.LogInformation("GPU: {gpu} (VRAM: {vram}, Driver: {driverVersion})",
            machineInformation.Gpus[0].Name,
            machineInformation.Gpus[0].AvailableVideoMemoryHRF,
            machineInformation.Gpus[0].DriverVersion
        );

        var skybox = fileParseService.Parse<TextureCubemap>(AssetPaths.Textures.At(@"skyboxes\stars01\"));
        renderSettings.Skybox.Set(skybox);
        renderSettings.AmbientLight.Set(Color.FromArgb(20, 21, 37).ToVector3());
        
        Shortcut feedbackShortcut = new(
            name: "Feedback form",
            category: "UI",
            ShortcutModifiers.None,
            Key.F8,
            isEnabled: Shortcut.DefaultEnabled,
            action: OpenFeedbackForm
        );
        shortcutService.RegisterShortcut(feedbackShortcut);
    }

    internal void Quit()
    {
        _windowContext.Close();
    }
    
    private void OpenFeedbackForm()
    {
        _modalMenu.GoToPage(FeedbackModal.Modal);
    }
}