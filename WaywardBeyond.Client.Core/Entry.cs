using HardwareInformation;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry : IAutoActivate
{
    private readonly IWindowContext _windowContext;

    public Entry(in ILogger<Entry> logger, in IShortcutService shortcutService, in IWindowContext windowContext)
    {
        _windowContext = windowContext;
        
        Shortcut quitShortcut = new(
            "Quit Game",
            "General",
            ShortcutModifiers.None,
            Key.Esc,
            Shortcut.DefaultEnabled,
            Quit
        );
        shortcutService.RegisterShortcut(quitShortcut);

        windowContext.SetTitle("Wayward Beyond");
        logger.LogInformation("Starting Wayward Beyond {version}", WaywardBeyond.Version);

        MachineInformation? machineInformation = MachineInformationGatherer.GatherInformation();
        if (machineInformation != null)
        {
            logger.LogInformation("OS: {os}", machineInformation.OperatingSystem.VersionString);
            logger.LogInformation("CPU: {cpu}", machineInformation.Cpu.Name); 
            logger.LogInformation("GPU: {gpu} (VRAM: {vram}, Driver: {driverVersion})",
                machineInformation.Gpus[0].Name,
                machineInformation.Gpus[0].AvailableVideoMemoryHRF,
                machineInformation.Gpus[0].DriverVersion
            );
        }
    }

    internal void Quit()
    {
        _windowContext.Close();
    }
}