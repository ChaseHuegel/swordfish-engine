using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
// ReSharper disable UnusedType.Global

namespace Swordfish.Library.IO;

public abstract class Command
{
    public abstract string Option { get; }
    public abstract string Description { get; }
    public abstract string ArgumentsHint { get; }
    public Command[] Subcommands { get; }
    private Command _parent;

    public Command() : this([]) { }

    public Command(Command[] commands)
    {
        Subcommands = commands;
        foreach (Command command in Subcommands)
        {
            command._parent = this;
        }
    }

    protected virtual Task<CommandState> InvokeAsync(ReadOnlyQueue<string> args)
    {
        return Task.FromResult(CommandState.Failure);
    }

    public async Task<CommandState> TryInvokeAsync(ReadOnlyQueue<string> args)
    {
        if (!args.TryTake(Option))
        {
            return CommandState.Failure;
        }

        if (await InvokeAsync(args) == CommandState.Success)
        {
            return CommandState.Success;
        }

        foreach (Command cmd in Subcommands)
        {
            if (await cmd.TryInvokeAsync(args) == CommandState.Success)
            {
                return CommandState.Success;
            }
        }

        return CommandState.Failure;
    }

    public string GetHelpText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(GetUsage());
        builder.Append("\t" + Description);

        return builder.ToString();
    }

    public string GetHint()
    {
        var builder = new StringBuilder(Option);
        if (!string.IsNullOrWhiteSpace(ArgumentsHint))
        {
            builder.Append(' ');
            builder.Append(ArgumentsHint);
        }

        return builder.ToString();
    }

    public string GetUsage()
    {
        var builder = new StringBuilder();

        if (_parent != null)
        {
            builder.Append(_parent.GetHint());
            builder.Append(' ');
        }

        builder.Append(GetHint());

        if (Subcommands.Length == 0)
        {
            //  If there is only a single subcommand, don't bother with formatting.
            string usage = Subcommands.First().GetUsage();
            if (string.IsNullOrWhiteSpace(usage))
            {
                return builder.ToString();
            }

            builder.Append(' ');
            builder.Append(usage);
        }
        else
        {
            //  Format subcommand options
            builder.Append(" (");
            for (var i = 0; i < Subcommands.Length; i++)
            {
                builder.Append(Subcommands[i].Option);
                if (i < Subcommands.Length - 1)
                {
                    builder.Append('|');
                }
            }
            builder.Append(')');

            //  Format argument hints
            builder.Append(" [");
            for (var i = 0; i < Subcommands.Length; i++)
            {
                builder.Append(Subcommands[i].ArgumentsHint);
                if (i < Subcommands.Length - 1)
                {
                    builder.Append('|');
                }
            }
            builder.Append(']');
        }

        return builder.ToString();
    }
}

public abstract class Command<TSub0>() : Command([new TSub0()])
    where TSub0 : Command, new();

public abstract class Command<TSub0, TSub1>() : Command([new TSub0(), new TSub1()])
    where TSub0 : Command, new()
    where TSub1 : Command, new();

public abstract class Command<TSub0, TSub1, TSub2>() : Command([new TSub0(), new TSub1(), new TSub2()])
    where TSub0 : Command, new()
    where TSub1 : Command, new()
    where TSub2 : Command, new();