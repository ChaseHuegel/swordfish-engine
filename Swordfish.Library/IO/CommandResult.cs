using System.Diagnostics.CodeAnalysis;

namespace Swordfish.Library.IO;

public readonly struct CommandResult
{
    public bool IsSuccessState => State == CommandState.Success;

    public readonly CommandState State;

    public readonly string OriginalString;

    [MaybeNull]
    public readonly Command Command;

    public CommandResult(CommandState state, string originalString, Command command)
    {
        State = state;
        OriginalString = originalString;
        Command = command;
    }
}