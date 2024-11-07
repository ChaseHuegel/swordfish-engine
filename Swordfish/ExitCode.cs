// ReSharper disable UnusedMember.Global
namespace Swordfish;

/// <summary>
///     Exit codes 1-1000 are reserved for use by Swordfish.
/// </summary>
public enum ExitCode
{
    /// <summary>
    ///     No issues.
    /// </summary>
    Normal = 0,
    
    //  1-100 is reserved for the underlying AppEngine.
    
    /// <summary>
    ///     An unexpected crash occurred.
    /// </summary>
    Crash = 101,
    
    /// <summary>
    ///     Reached an unexpected state. This is a fatal error.
    /// </summary>
    BadState = 102,
}