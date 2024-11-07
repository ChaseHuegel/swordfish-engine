using System;
using Swordfish.Library.Util;

namespace Swordfish.Library.Diagnostics;

public static class Safe
{
    /// <summary>
    ///     Tries to run an action, catching and logging exceptions.
    /// </summary>
    /// <returns>True if successful; otherwise false.</returns>
    public static Result<Exception> Invoke(Action action)
    {
        try
        {
            action.Invoke();
            return new Result<Exception>(success: true, null);
        }
        catch (Exception ex)
        {
            return new Result<Exception>(success: false, ex);
        }
    }
}