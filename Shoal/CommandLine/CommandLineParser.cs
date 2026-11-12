using System.Text;
using Swordfish.Compilation.Lexing;

namespace Shoal.CommandLine;

public class CommandLineParser
{
    private static readonly List<TokenMatcher<CommandLineToken>> _tokenMatchers = new()
    {
        new TokenMatcher<CommandLineToken>(CommandLineToken.EqualsOrWhitespace, @"\G[\s\t\n\r\f\0=]+"),
        new TokenMatcher<CommandLineToken>(CommandLineToken.Tack, @"\G-{1,2}"),
        new TokenMatcher<CommandLineToken>(CommandLineToken.Text, """\G([^\s-""'=]+)"""),
        new TokenMatcher<CommandLineToken>(CommandLineToken.DoubleQuoteValue, @"\G""[^""]*"""),
        new TokenMatcher<CommandLineToken>(CommandLineToken.SingleQuoteValue, @"\G'[^']*'"),
    };

    private readonly Lexer<CommandLineToken> _lexer = new(_tokenMatchers);
    private readonly CommandLineTokenParser _tokenParser = new();

    public CommandLineArgs Parse(string[] args)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            AppendArg(builder, args[i]);
        }

        return Parse(builder.ToString());
    }

    /// <summary>
    /// Rejoins one argv entry into the command-line string. An entry that holds whitespace is quoted, so the lexer keeps it as a single value.
    /// </summary>
    private static void AppendArg(StringBuilder builder, string arg)
    {
        if (!arg.Any(char.IsWhiteSpace))
        {
            builder.Append(arg);
            return;
        }

        if (!arg.Contains('"'))
        {
            builder.Append('"');
            builder.Append(arg);
            builder.Append('"');
            return;
        }

        if (!arg.Contains('\''))
        {
            builder.Append('\'');
            builder.Append(arg);
            builder.Append('\'');
            return;
        }

        builder.Append(arg);
    }
    
    public CommandLineArgs Parse(string input)
    {
        List<Token<CommandLineToken>> tokens = _lexer.Lex(input);
        return tokens.Count == 0 ? CommandLineArgs.Empty : _tokenParser.Parse(tokens);
    }
}