using System;

namespace Swordfish.Library.Diagnostics;

public class FatalAlertException : Exception
{
    public FatalAlertException(string message) : base("Something went horribly wrong! " + message) {}
}