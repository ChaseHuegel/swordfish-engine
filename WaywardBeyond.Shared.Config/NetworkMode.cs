using System;
using Shoal.CommandLine;

namespace WaywardBeyond.Shared.Config;

/// <summary>
/// How this process participates in a network session. <see cref="Host"/> runs the authoritative in-process
/// server (singleplayer, open to LAN) and connects its own local player through the in-process transport.
/// <see cref="Client"/> skips running a server entirely — it joins a remote host over a socket and is
/// resolved from <see cref="Shoal.CommandLine.CommandLineArgs"/> (default is <see cref="Host"/>, so existing
/// singleplayer behavior is unchanged).
/// </summary>
public enum NetworkMode
{
    Host,
    Client,
}

public static class NetworkModeResolver
{
    /// <summary>The <c>--client</c> command-line flag / env key that selects join mode.</summary>
    public const string ClientKey = "client";

    /// <summary>Resolves the network mode, honoring the <c>--client</c>/<c>WB_CLIENT</c> override.</summary>
    public static NetworkMode Resolve(in CommandLineArgs? args = null)
    {
        try
        {
            if (args != null && args.GetFlag(ClientKey)
                || Environment.GetEnvironmentVariable($"WB_{ClientKey}") is string env && !string.IsNullOrEmpty(env))
            {
                return NetworkMode.Client;
            }
        }
        catch
        {
            //  A malformed environment never forces client mode; fall through to host.
        }

        return NetworkMode.Host;
    }
}