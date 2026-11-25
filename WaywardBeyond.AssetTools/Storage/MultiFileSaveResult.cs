using Swordfish.Library.Util;

namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Outcome of a multi-file transactional save including conflicts and rollback state.
/// </summary>
public readonly record struct MultiFileSaveResult(
    bool Success,
    IReadOnlyList<string> WrittenPaths,
    IReadOnlyList<FileSaveConflict> Conflicts,
    bool RolledBack,
    IReadOnlyList<string> RolledBackPaths,
    string? ErrorMessage,
    Exception? Exception)
{
    /// <summary>
    ///     Creates a successful multi-file save outcome.
    /// </summary>
    public static MultiFileSaveResult FromSuccess(IReadOnlyList<string> writtenPaths)
    {
        return new MultiFileSaveResult(true, writtenPaths, [], false, [], null, null);
    }

    /// <summary>
    ///     Creates a failed outcome caused by one or more external file conflicts before writing.
    /// </summary>
    public static MultiFileSaveResult FromConflicts(IReadOnlyList<FileSaveConflict> conflicts)
    {
        string message = $"External conflict detected on {conflicts.Count} file(s).";
        return new MultiFileSaveResult(false, [], conflicts, false, [], message, null);
    }

    /// <summary>
    ///     Creates a failed outcome after rolling back previously applied files.
    /// </summary>
    public static MultiFileSaveResult FromRollback(
        bool rolledBack,
        IReadOnlyList<string> rolledBackPaths,
        string errorMessage,
        Exception? exception = null)
    {
        return new MultiFileSaveResult(false, [], [], rolledBack, rolledBackPaths, errorMessage, exception);
    }

    /// <summary>
    ///     Creates a failed outcome before any file mutation began.
    /// </summary>
    public static MultiFileSaveResult FromFailure(string errorMessage, Exception? exception = null)
    {
        return new MultiFileSaveResult(false, [], [], false, [], errorMessage, exception);
    }

    /// <summary>
    ///     Converts this save outcome to a standard <see cref="Result"/>.
    /// </summary>
    public Result ToResult()
    {
        if (Success)
        {
            return Result.FromSuccess();
        }

        return Exception != null
            ? Result.FromFailure(Exception)
            : Result.FromFailure(ErrorMessage);
    }
}
