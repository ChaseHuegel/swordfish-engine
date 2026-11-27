using Shoal.CrashHandling;
using Shoal.Modularity;
using Swordfish;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Webhooks;

namespace WaywardBeyond.Client.Launcher;

internal sealed class Application(in string[] args)
{
    private const string COMPANY_NAME = "bitfish";

    private readonly string[] _args = args;

    public int Run()
    {
        AppPaths appPaths = SwordfishEngine.ResolveAppPaths(_args, companyName: COMPANY_NAME);
        ModuleResolutionOptions moduleResolution = PackModuleResolution.CreateOptions(appPaths);
        var options = new CrashHandlerOptions
        {
            AppName = "Wayward Beyond",
            AppVersion = typeof(Application).Assembly.GetName().Version?.ToString() ?? SwordfishEngine.Version.ToString(),
            Headless = false,
            AppPaths = appPaths,
            OnSendReportAsync = SendCrashReportAsync,
        };

        return CrashSupervisor.Run(_args, options, appArgs =>
        {
            var engine = new SwordfishEngine(appArgs, appPaths, moduleResolution: moduleResolution);
            return engine.Run();
        });
    }

    private static async Task<Result<string>> SendCrashReportAsync(CrashReportSubmission submission, CancellationToken cancellationToken)
    {
        var webhookService = new WebhookService(Webhooks.Default);
        var feedbackWebhook = new FeedbackWebhook(webhookService);

        Result<Guid> result = await feedbackWebhook.SendCrashAsync(
            description: submission.Description,
            contact: submission.Contact,
            faultSummary: submission.Report.FaultSummary,
            reportPath: submission.Report.ReportPath,
            sessionLogPath: submission.Report.SessionLogPath,
            dumpPath: submission.Report.DumpPath,
            crashReportJsonPath: submission.Report.CrashReportJsonPath,
            maxUploadBytes: submission.Report.MaxUploadBytes,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        return result.Success
            ? Result<string>.FromSuccess(result.Value.ToString())
            : new Result<string>(success: false, string.Empty, result.Message, result.Exception);
    }
}
