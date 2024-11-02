using Swordfish.Compilation.Lexing;
using Swordfish.Compilation.Linting;
using Swordfish.Compilation.Parsing;

namespace Shoal.CommandLine;

internal class CommandLineTokenParser : TokenParser<CommandLineToken, CommandLineArgs>
{
    private readonly List<string> _flags = [];
    private readonly List<KeyValuePair<string, string[]>> _options = [];
    
    protected override CommandLineArgs CreateResult()
    {
        return new CommandLineArgs(_flags.ToArray(), _options.ToArray());
    }
    
    protected override Issue[] GetIssues()
    {
        return [];
    }
    
    protected override bool ReadNext()
    {
        bool readToken = false;
        readToken |= TryDiscardToken(CommandLineToken.EqualsOrWhitespace);
        readToken |= TryReadArg();
        return readToken;
    }

    private bool TryReadArg()
    {
        if (!TryReadToken(CommandLineToken.Tack, out Token<CommandLineToken> _))
        {
            return false;
        }
        
        string key = ReadToken(CommandLineToken.Text).Value;

        List<string> values = [];
        while (TryDiscardToken(CommandLineToken.EqualsOrWhitespace) && TryReadQuoteOrText(out Token<CommandLineToken> valueToken))
        {
            values.Add(valueToken.Value);
        }

        if (values.Count == 0)
        {
            _flags.Add(key);
            return true;
        }

        _options.Add(new KeyValuePair<string, string[]>(key, values.ToArray()));
        return true;
    }
    
    private bool TryReadQuoteOrText(out Token<CommandLineToken> token)
    {
        if (TryReadToken(CommandLineToken.DoubleQuoteValue, out token))
        {
            return true;
        }
        
        if (TryReadToken(CommandLineToken.SingleQuoteValue, out token))
        {
            return true;
        }

        return TryReadToken(CommandLineToken.Text, out token);
    }
}