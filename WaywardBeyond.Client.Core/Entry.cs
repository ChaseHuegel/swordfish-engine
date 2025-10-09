using System.Numerics;
using System.Text;
using HardwareInformation;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.UI.Reef;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry : IAutoActivate
{
    private readonly IWindowContext _windowContext;
    private readonly ReefContext _reefContext;

    public Entry(
        in ILogger<Entry> logger,
        in IShortcutService shortcutService,
        in IWindowContext windowContext,
        in ReefContext reefContext
    ) {
        _windowContext = windowContext;
        _reefContext = reefContext;
        
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
        
        windowContext.Update += OnWindowUpdate;
    }

    private double _currentTime;
    private void OnWindowUpdate(double delta)
    {
        _currentTime += delta;
        UIBuilder<Material> ui = _reefContext.Builder;

        if (WaywardBeyond.GameState == GameState.Loading)
        {
            using (ui.Element())
            {
                ui.Color = new Vector4(0.25f, 0.25f, 0.25f, 0.75f);
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };

                var statusBuilder = new StringBuilder("Loading");
                int steps = MathS.WrapInt((int)(_currentTime * 2d), 0, 3);
                for (var i = 0; i < steps; i++)
                {
                    statusBuilder.Append('.');
                }

                using (ui.Text(statusBuilder.ToString()))
                {
                    ui.FontSize = 30;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                        X = new Relative(0.5f),
                        Y = new Relative(0.5f),
                    };
                }
            }
        }
    }

    internal void Quit()
    {
        _windowContext.Close();
    }
}