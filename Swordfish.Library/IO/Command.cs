using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swordfish.Library.Collections;

namespace Swordfish.Library.IO
{
    public abstract class Command
    {
        public abstract string Option { get; }
        public abstract string Description { get; }
        public abstract string ArgumentsHint { get; }
        public Command[] Subcommands { get; private set; } = Array.Empty<Command>();
        private Command Parent;

        public Command()
            : this(Array.Empty<Command>()) { }

        public Command(Command[] commands)
        {
            Subcommands = commands;
            foreach (Command command in Subcommands)
                command.Parent = this;
        }

        protected virtual Task<CommandState> InvokeAsync(ReadOnlyQueue<string> args)
        {
            return Task.FromResult(CommandState.Failure);
        }

        public async Task<bool> TryInvokeAsync(ReadOnlyQueue<string> args)
        {
            if (!args.TryTake(Option))
                return false;

            if (await InvokeAsync(args) == CommandState.Success)
                return true;

            foreach (var cmd in Subcommands)
            {
                if (await cmd.TryInvokeAsync(args))
                    return true;
            }

            return false;
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

            if (Parent != null)
            {
                builder.Append(Parent.GetHint());
                builder.Append(' ');
            }

            builder.Append(GetHint());

            if (Subcommands.Length == 0)
            {
                //  If there is only a single subcommand, don't bother with formatting.
                string usage = Subcommands.First().GetUsage();
                if (!string.IsNullOrWhiteSpace(usage))
                {
                    builder.Append(' ');
                    builder.Append(usage);
                }
            }
            else
            {
                //  Format subcommand options
                builder.Append(" (");
                for (int i = 0; i < Subcommands.Length; i++)
                {
                    builder.Append(Subcommands[i].Option);
                    if (i < Subcommands.Length - 1)
                        builder.Append('|');
                }
                builder.Append(')');

                //  Format argument hints
                builder.Append(" [");
                for (int i = 0; i < Subcommands.Length; i++)
                {
                    builder.Append(Subcommands[i].ArgumentsHint);
                    if (i < Subcommands.Length - 1)
                        builder.Append('|');
                }
                builder.Append(']');
            }

            return builder.ToString();
        }
    }

    public abstract class Command<TSub0> : Command
        where TSub0 : Command, new()
    {
        public Command()
            : base(new Command[] { new TSub0() }) { }
    }

    public abstract class Command<TSub0, TSub1> : Command
        where TSub0 : Command, new()
        where TSub1 : Command, new()
    {
        public Command()
            : base(new Command[] { new TSub0(), new TSub1() }) { }
    }

    public abstract class Command<TSub0, TSub1, TSub2> : Command
        where TSub0 : Command, new()
        where TSub1 : Command, new()
        where TSub2 : Command, new()
    {
        public Command()
            : base(new Command[] { new TSub0(), new TSub1(), new TSub2() }) { }
    }
}
