using System.Diagnostics.CodeAnalysis;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.IO;

public readonly struct CommandResult(in CommandState state, in string originalString, in Command command)
{
    public bool IsSuccessState => State == CommandState.Success;

    public readonly CommandState State = state;

    public readonly string OriginalString = originalString;

    [MaybeNull]
    public readonly Command Command = command;
}