using System.Runtime.InteropServices;
using DryIoc;
using Shoal;
using Shoal.CommandLine;
using WaywardBeyond.Config;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Server;

namespace WaywardBeyond.Server.Launcher;

/// <summary>Headless dedicated server entry point.</summary>
internal static class Program
{
    private static readonly ManualResetEventSlim _shutdown = new();

    private static int Main(string[] args)
    {
        AppEngine engine = AppEngine.Build(args, RegisterDedicatedServices);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _shutdown.Set();
        };
        using PosixSignalRegistration sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            _shutdown.Set();
        });

        try
        {
            engine.Start();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start the dedicated server: {ex.Message}");
            engine.Dispose();
            return 1;
        }

        if (engine.Container.Resolve<LanHostInfo>().ListenPort == 0)
        {
            Console.Error.WriteLine("Failed to start the dedicated server: the LAN listener did not bind.");
            engine.Dispose();
            return 1;
        }

        Console.WriteLine("Dedicated server started. Press Ctrl+C to stop.");

        _shutdown.Wait();
        engine.Dispose();
        Console.WriteLine("Dedicated server stopped.");
        return 0;
    }

    private static void RegisterDedicatedServices(IContainer container)
    {
        //  The shared host wire-up: registry, serializers, hub (no loopback), save storage, and config.
        //  The server module's own injector adds ServerWorldHost + LanHost.
        HostComposition.RegisterNetworking(container, seedLocalLoopback: false);

        //  Headless interaction content: breaks and loot work; place requires client item content.
        container.Register<IInteractionContent, ServerInteractionContent>(Reuse.Singleton);

        ApplyCliOverrides(container);
    }

    private static void ApplyCliOverrides(IContainer container)
    {
        CommandLineArgs args = container.Resolve<CommandLineArgs>();

        NetworkingConfig networkingConfig = container.Resolve<NetworkingConfig>();
        bool networkingChanged = false;
        if (args.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            networkingConfig.Server.Name.Set(name);
            networkingChanged = true;
        }
        if (args.TryGetValue("port", out string? port) && int.TryParse(port, out int parsedPort) && parsedPort is >= 1 and <= 65535)
        {
            networkingConfig.Server.Port.Set(parsedPort);
            networkingChanged = true;
        }
        if (networkingChanged)
        {
            networkingConfig.Save();
        }

        StorageConfig storageConfig = container.Resolve<StorageConfig>();
        if (args.TryGetValue("data", out string? dataRoot) && !string.IsNullOrWhiteSpace(dataRoot))
        {
            storageConfig.SaveRoot.Set(dataRoot);
            storageConfig.Save();
        }
    }
}
