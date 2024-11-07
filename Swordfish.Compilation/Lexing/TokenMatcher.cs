using System.Text.RegularExpressions;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace Swordfish.Compilation.Lexing;

// ReSharper disable once ClassNeverInstantiated.Global
public class TokenMatcher<T>(in T type, in string regexPattern) where T : struct
{
    private readonly Regex _regex = new(regexPattern);

    private readonly T _type = type;

    public Match<T> Match(string input, int startIndex = 0)
    {
        RegexMatch match = _regex.Match(input, startIndex);
        if (!match.Success)
        {
            return default;
        }

        var token = new Token<T>(_type, match.Value);
        return new Match<T>(true, match.Length, token);
    }
}