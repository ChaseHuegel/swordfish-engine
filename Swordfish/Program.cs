using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Swordfish;

public static class Program
{
    static int Main(string[] args)
    {
        var engine = new SwordfishEngine();
        return engine.Run(args);
    }
}