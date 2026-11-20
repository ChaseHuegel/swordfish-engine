using System.Runtime.InteropServices;

namespace Shoal.CrashHandling;

/// <summary>
///     Attaches or allocates a Windows console for supervised --debug sessions so piped child output stays visible.
/// </summary>
internal static class WindowsConsoleHelper
{
    private const uint ATTACH_PARENT_PROCESS = unchecked((uint)-1);

    /// <summary>
    ///     Attaches to the parent console or allocates a new console on Windows.
    /// </summary>
    public static bool TryAttachOrAllocate()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (!AttachConsole(ATTACH_PARENT_PROCESS) && !AllocConsole())
        {
            return false;
        }

        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}
