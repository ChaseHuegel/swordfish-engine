using System;
using System.Collections.Generic;

namespace Swordfish.Library.Diagnostics;

public interface ILogListener
{
    event EventHandler<LoggerEventArgs> NewLog;

    LoggerEventArgs[] GetHistory();
}