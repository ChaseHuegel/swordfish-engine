using System.Net;

namespace WaywardBeyond.Shared.Config;

public interface IConfiguration
{
    string? GetString(string key);
    
    IPAddress? GetIPAddress(string key);
    
    int? GetInt(string key);
}