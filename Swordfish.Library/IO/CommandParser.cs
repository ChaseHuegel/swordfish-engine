using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swordfish.Library.Collections;

namespace Swordfish.Library.IO;

public class CommandParser(params Command[] commands)
{
    private readonly List<Command> _commands = [..commands];
    private readonly char _indicator;

    public CommandParser(char indicator, params Command[] commands)
        : this(commands)
    {
        _indicator = indicator;
    }

    public string GetHelpText()
    {
        var builder = new StringBuilder();
        foreach (Command command in _commands)
        {
            builder.AppendLine(_indicator + command.GetHelpText());
        }

        return builder.ToString();
    }

    public async Task<CommandResult> TryRunAsync(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new CommandResult(CommandState.Failure, line, null);
        }

        if (_indicator != default && !line.StartsWith(_indicator))
        {
            return new CommandResult(CommandState.Failure, line, null);
        }

        int lineStart = _indicator != default ? 1 : 0;
        string[] parts = line[lineStart..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(str => str.Trim()).ToArray();

        var args = new ReadOnlyQueue<string>(parts);

        foreach (Command command in _commands)
        {
            if (await command.TryInvokeAsync(args) == CommandState.Success)
            {
                return new CommandResult(CommandState.Success, line, command);
            }
        }

        return new CommandResult(CommandState.Failure, line, null);
    }
}