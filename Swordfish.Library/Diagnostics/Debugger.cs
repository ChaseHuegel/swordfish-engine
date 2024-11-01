using System;
using System.Diagnostics;
using System.IO;
using Swordfish.Library.Util;

namespace Swordfish.Library.Diagnostics
{
    public static class Debugger
    {
        /// <summary>
        /// Tries to run an action, catching and logging exceptions.
        /// </summary>
        /// <param name="action">The action to run.</param>
        /// <param name="message">Optional message to log if an exception was caught.</param>
        /// <returns>True if successful; otherwise false.</returns>
        public static Result<Exception> SafeInvoke(Action action)
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
}