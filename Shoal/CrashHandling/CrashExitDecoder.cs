namespace Shoal.CrashHandling;

/// <summary>
///     Decodes platform-specific process exit codes into human-readable crash descriptions.
/// </summary>
public static class CrashExitDecoder
{
    /// <summary>
    ///     Describes a non-zero exit code for the current operating system.
    /// </summary>
    public static string Describe(int exitCode)
    {
        return Describe(exitCode, OperatingSystem.IsWindows(), OperatingSystem.IsLinux() || OperatingSystem.IsMacOS());
    }

    /// <summary>
    ///     Describes a non-zero exit code for the specified platform flags.
    /// </summary>
    public static string Describe(int exitCode, bool isWindows, bool isUnix)
    {
        if (exitCode == 0)
        {
            return "Normal exit (0)";
        }

        if (isWindows)
        {
            uint code = unchecked((uint)exitCode);
            string? ntStatus = code switch
            {
                0xC0000005 => "STATUS_ACCESS_VIOLATION (Access violation)",
                0xC00000FD => "STATUS_STACK_OVERFLOW (Stack overflow)",
                0xC0000094 => "STATUS_INTEGER_DIVIDE_BY_ZERO (Integer divide by zero)",
                0xC000001D => "STATUS_ILLEGAL_INSTRUCTION (Illegal instruction)",
                0xC0000409 => "STATUS_STACK_BUFFER_OVERRUN (Fast fail / stack buffer overrun)",
                0xC0000374 => "STATUS_HEAP_CORRUPTION (Heap corruption)",
                0xC000013A => "STATUS_CONTROL_C_EXIT (Terminated via Ctrl+C)",
                0xE0434352 => "CLR_UNHANDLED_EXCEPTION (Unhandled managed .NET exception)",
                0x80131623 => "COR_E_FAILFAST (Environment.FailFast)",
                _ => null,
            };

            return ntStatus != null
                ? $"0x{code:X8} ({exitCode}) — {ntStatus}"
                : $"Exit code {exitCode} (0x{code:X8})";
        }

        if (isUnix && exitCode > 128 && exitCode <= 192)
        {
            int signal = exitCode - 128;
            string? signalName = signal switch
            {
                2 => "SIGINT (Interrupt)",
                4 => "SIGILL (Illegal instruction)",
                6 => "SIGABRT (Aborted)",
                7 => "SIGBUS (Bus error)",
                8 => "SIGFPE (Floating point exception)",
                9 => "SIGKILL (Killed)",
                11 => "SIGSEGV (Segmentation fault)",
                15 => "SIGTERM (Terminated)",
                31 => "SIGSYS (Bad system call)",
                _ => null,
            };

            return signalName != null
                ? $"Signal {signal} ({signalName}, exit code {exitCode})"
                : $"Signal {signal} (exit code {exitCode})";
        }

        return $"Exit code {exitCode}";
    }
}
