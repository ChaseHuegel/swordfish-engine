namespace Swordfish;

public enum ExitCode
{
    /// <summary>
    ///     No issues.
    /// </summary>
    Normal = 0,
    
    /// <summary>
    ///     Reached an unexpected state. This is a fatal error.
    /// </summary>
    BadState = 1,
    
    /// <summary>
    ///     An unexpected crash occurred.
    /// </summary>
    Crash = 2,
}