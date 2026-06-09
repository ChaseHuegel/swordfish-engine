using System;
using System.Net;
using Shoal.CommandLine;

namespace WaywardBeyond.Shared.Config;

public sealed class EnvCmdConfiguration(in CommandLineArgs args) : IConfiguration
{
    private readonly CommandLineArgs _args = args;

    public string? GetString(string key)
    {
        string? value = Environment.GetEnvironmentVariable(key);
        if (value == null)
        {
            _args.TryGetValue(key, out value);
        }
        
        return value;
    }

    public IPAddress? GetIPAddress(string key)
    {
        string? str = GetString(key);
        bool parsed = IPAddress.TryParse(str, out IPAddress? value);
        return parsed ? value : null;
    }
    
    public int? GetInt(string key)
    {
        string? str = GetString(key);
        bool parsed = int.TryParse(str, out int value);
        return parsed ? value : null;
    }
}
