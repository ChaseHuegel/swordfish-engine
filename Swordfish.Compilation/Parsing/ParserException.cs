using System;
using System.Collections.Generic;

namespace Swordfish.Compilation.Parsing;

public class ParserException : AggregateException
{
    public ParserException(string message, IEnumerable<Exception> exceptions) : base(message, exceptions) { }

    public ParserException(string message, params Exception[] exceptions) : base(message, exceptions) { }
}