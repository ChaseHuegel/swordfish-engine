using System;

namespace Swordfish.Library.Diagnostics;

public class FatalAlertException(string message) : Exception("Something went horribly wrong! " + message);