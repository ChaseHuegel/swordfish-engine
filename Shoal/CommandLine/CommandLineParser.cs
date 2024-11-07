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
        string input = string.Join(' ', args);
        return Parse(input);
    }
    
    public CommandLineArgs Parse(string input)
    {
        List<Token<CommandLineToken>> tokens = _lexer.Lex(input);
        return tokens.Count == 0 ? CommandLineArgs.Empty : _tokenParser.Parse(tokens);
    }
}