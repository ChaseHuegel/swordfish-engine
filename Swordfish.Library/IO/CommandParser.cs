using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swordfish.Library.Collections;

namespace Swordfish.Library.IO
{
    public class CommandParser
    {
        private readonly List<Command> Commands;
        private readonly char Indicator;

        public CommandParser(params Command[] commands)
        {
            Commands = new List<Command>(commands);
        }

        public CommandParser(char indicator, params Command[] commands)
            : this(commands)
        {
            Indicator = indicator;
        }

        public string GetHelpText()
        {
            var builder = new StringBuilder();
            foreach (Command command in Commands)
                builder.AppendLine(Indicator + command.GetHelpText());

            return builder.ToString();
        }

        public async Task<CommandResult> TryRunAsync(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return new CommandResult(CommandState.Failure, line, null);

            if (Indicator != default && !line.StartsWith(Indicator))
                return new CommandResult(CommandState.Failure, line, null);

            var lineStart = Indicator != default ? 1 : 0;
            var parts = line[lineStart..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(str => str.Trim()).ToArray(); //  StringSplitOptions.TrimEntries is only available in net5.0+

            var args = new ReadOnlyQueue<string>(parts);

            foreach (Command command in Commands)
            {
                if (await command.TryInvokeAsync(args) == CommandState.Success)
                    return new CommandResult(CommandState.Success, line, command);
            }

            return new CommandResult(CommandState.Failure, line, null);
        }
    }
}
