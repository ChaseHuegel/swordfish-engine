namespace Swordfish.AppEngine;

/// <summary>
///     Exit codes 1-100 are reserved for use by AppEngine.
/// </summary>
public enum ExitCode
{
    /// <summary>
    ///     No issues.
    /// </summary>
    Normal = 0,
    
    /// <summary>
    ///     One or more dependencies are failing.
    /// </summary>
    BadDependency = 1,
}