using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Services;

internal sealed class ExternalAppService(in WebhookService webhookService)
{
    private readonly WebhookService _webhookService = webhookService;

    public async Task TryOpenDiscordAsync()
    {
        Result<Uri> uri = await _webhookService.ResolveDiscordUriAsync();
        if (!uri.Success)
        {
            return;
        }
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = uri.Value.ToString(),
            UseShellExecute = true,
            Verb = "open",
        };

        Process? process = Process.Start(processStartInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            process.Dispose();
        }
    }

    public async Task TryOpenSteamAsync()
    {
        Result<Uri> uri = await _webhookService.ResolveSteamUriAsync();
        if (!uri.Success)
        {
            return;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = uri.Value.ToString(),
            UseShellExecute = true,
            Verb = "open",
        };

        Process? process = Process.Start(processStartInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            process.Dispose();
        }
    }
}