using Swordfish.Library.Util;

namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Outcome of an atomic single-file save operation including any external conflict details.
/// </summary>
public readonly record struct AtomicSaveResult(
    bool Success,
    string? WrittenSha256,
    FileSaveConflict? Conflict,
    string? ErrorMessage,
    Exception? Exception)
{
    /// <summary>
    ///     Creates a successful single-file save result with the verified SHA-256 digest.
    /// </summary>
    public static AtomicSaveResult FromSuccess(string writtenSha256)
    {
        return new AtomicSaveResult(true, writtenSha256, null, null, null);
    }

    /// <summary>
    ///     Creates a failed save result representing an external disk modification conflict.
    /// </summary>
    public static AtomicSaveResult FromConflict(in FileSaveConflict conflict)
    {
        string message = $"External conflict ({conflict.Reason}) on '{conflict.FilePath}'.";
        return new AtomicSaveResult(false, null, conflict, message, null);
    }

    /// <summary>
    ///     Creates a failed save result from an error message.
    /// </summary>
    public static AtomicSaveResult FromFailure(string errorMessage)
    {
        return new AtomicSaveResult(false, null, null, errorMessage, null);
    }

    /// <summary>
    ///     Creates a failed save result from an exception.
    /// </summary>
    public static AtomicSaveResult FromFailure(Exception exception)
    {
        return new AtomicSaveResult(false, null, null, exception.Message, exception);
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
