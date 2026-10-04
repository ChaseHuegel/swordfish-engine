using System;
using System.Runtime.InteropServices;
using System.Threading;
using DryIoc;
using Shoal;
using Shoal.CommandLine;
using Shoal.DependencyInjection;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Streaming;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Gameplay;

namespace WaywardBeyond.Server.Launcher;

/// <summary>
/// Headless dedicated server entry point: builds the Shoal application with the server and shared
/// modules only (no client module - no window, input, or client-world services), registers the shared
/// host composition and the dedicated interaction content, starts NATS, runs the server + LAN host,
/// and shuts down cleanly on Ctrl+C or SIGTERM (world flush, session teardown, NATS stop).
/// </summary>
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
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => _shutdown.Set());

        try
        {
            engine.Start();
            Console.WriteLine("Dedicated server started. Press Ctrl+C to stop.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start the dedicated server: {ex.Message}");
            engine.Dispose();
            return 1;
        }

        _shutdown.Wait();
        engine.Dispose();
        Console.WriteLine("Dedicated server stopped.");
        return 0;
    }

    private static void RegisterDedicatedServices(IContainer container)
    {
        //  The shared host wire-up: registry, serializers, hub (no loopback), NATS-backed persistence,
        //  networking + physics config. The server module's own injector adds ServerContext + LanHost.
        HostComposition.RegisterNetworking(container, seedLocalLoopback: false);

        //  Headless interaction content: breaks and loot work; place requires client item content.
        container.Register<IInteractionContent, ServerInteractionContent>(Reuse.Singleton);

        //  CLI overrides for the default LAN configuration.
        CommandLineArgs args = container.Resolve<CommandLineArgs>();
        NetworkingSettings settings = container.Resolve<NetworkingSettings>();
        if (args.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            settings.ServerName.Set(name);
        }
        if (args.TryGetValue("port", out string? port) && int.TryParse(port, out int parsedPort) && parsedPort is >= 1 and <= 65535)
        {
            settings.ServerPort.Set(parsedPort);
        }
        settings.Save();

        container.Register<DedicatedServerEntry>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, DedicatedServerEntry>();
    }
}

/// <summary>Starts the embedded NATS child on boot; disposal stops it (see #0022).</summary>
internal sealed class DedicatedServerEntry(in PersistentNatsProcess natsProcess) : IAutoActivate, IDisposable
{
    private readonly PersistentNatsProcess _natsProcess = natsProcess;

    public void Run()
    {
        Result start = _natsProcess.Start();
        if (!start.Success)
        {
            Console.Error.WriteLine($"Failed to start the embedded NATS server: {start.Message}");
        }
    }

    public void Dispose()
    {
        _natsProcess.Dispose();
    }
}