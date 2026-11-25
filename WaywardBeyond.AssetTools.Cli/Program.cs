namespace WaywardBeyond.AssetTools.Cli;

/// <summary>
///     Command-line entry point for the Wayward Beyond Asset Tools CLI.
/// </summary>
public static class Program
{
    /// <summary>
    ///     Executes the CLI against <paramref name="args"/> and returns the process exit code.
    /// </summary>
    public static int Main(string[] args)
    {
        bool useColor = !Console.IsOutputRedirected;
        var runner = new CliRunner(Console.Out, Console.Error, useColor);
        return runner.Run(args);
    }
}
