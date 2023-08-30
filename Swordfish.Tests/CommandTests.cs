using System;
using System.Threading.Tasks;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class CommandTests : TestBase
{
    public CommandTests(ITestOutputHelper output) : base(output)
    {
    }

    private readonly CommandParser CommandParser = new('/', new HomeCommand());

    [Fact]
    public void TestHelpTextOutput()
    {
        Output.WriteLine(CommandParser.GetHelpText());
    }

    [Theory]
    [InlineData("/home set myhome", CommandState.Success)]
    [InlineData("home set myhome", CommandState.Failure)]
    [InlineData("this isn't a command", CommandState.Failure)]
    public async Task TestCommand(string command, CommandState expectedResult)
    {
        var res = (await CommandParser.TryRunAsync(command)).State;
        Assert.Equal(expectedResult, res);
    }

    public class HomeCommand : Command<HomeTpCommand, HomeSetCommand, HomeDeleteCommand>
    {
        public override string Option => "home";
        public override string Description => "Manage your home teleports.";
        public override string ArgumentsHint => "";
    }

    public class HomeSetCommand : Command
    {
        public override string Option => "set";
        public override string Description => "Sets a home teleport.";
        public override string ArgumentsHint => "<name>";

        protected override Task<CommandState> InvokeAsync(ReadOnlyQueue<string> args)
        {
            string name = args.Take();
            Console.WriteLine($"Home set to {name}");
            return Task.FromResult(CommandState.Success);
        }
    }

    public class HomeTpCommand : Command
    {
        public override string Option => "tp";
        public override string Description => "Teleports to one of your homes.";
        public override string ArgumentsHint => "<name>";

        protected override Task<CommandState> InvokeAsync(ReadOnlyQueue<string> args)
        {
            string name = args.Take();
            Console.WriteLine($"Home tp to {name}");
            return Task.FromResult(CommandState.Success);
        }
    }

    public class HomeDeleteCommand : Command
    {
        public override string Option => "delete";
        public override string Description => "Delete home teleports.";
        public override string ArgumentsHint => "(<name>|all)";

        protected override Task<CommandState> InvokeAsync(ReadOnlyQueue<string> args)
        {
            string arg1 = args.Take();

            if (arg1.Equals("all", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"Home delete all");
            else
                Console.WriteLine($"Home delete {arg1}");

            return Task.FromResult(CommandState.Success);
        }
    }
}
