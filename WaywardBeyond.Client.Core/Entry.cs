using System.Drawing;
using System.Numerics;
using HardwareInformation;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Physics;
using Swordfish.Settings;

namespace WaywardBeyond.Client.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class Entry : IAutoActivate
{
    private readonly IWindowContext _windowContext;

    public Entry(
        in ILogger<Entry> logger,
        in IWindowContext windowContext,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings,
        in IFileParseService fileParseService,
        in IPhysics physics
    ) {
        _windowContext = windowContext;

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
   
        physics.SetGravity(Vector3.Zero);
    }

    internal void Quit()
    {
        _windowContext.Close();
    }
}