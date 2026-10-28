using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal enum RssTokenKind
{
    Identifier,
    Number,
    Hash,
    String,
    Variable,
    Dot,
    Colon,
    Comma,
    LParen,
    RParen,
    LBrace,
    RBrace,
    Semicolon,
    Pipe,
    Whitespace,
    End,
}

internal readonly struct RssToken
{
    public readonly RssTokenKind Kind;
    public readonly string Text;
    public readonly int Offset;
    public readonly int Length;
    public readonly int Line;
    public readonly int Column;

    public RssToken(RssTokenKind kind, string text, int offset, int length, int line, int column)
    {
        Kind = kind;
        Text = text;
        Offset = offset;
        Length = length;
        Line = line;
        Column = column;
    }

    public bool IsTrivia => Kind == RssTokenKind.Whitespace;
}

internal enum RssPseudoState
{
    None,
    Hover,
    Active,
    Focus,
    Disabled,
}

internal sealed class RssDeclaration
{
    public string Name = string.Empty;
    public readonly List<RssToken> Value = [];
    public Location Location = Location.None;
}

internal sealed class RssVariable
{
    public string Name = string.Empty;
    public bool IsOverride;
    public readonly List<RssToken> Value = [];
    public Location Location = Location.None;
}

internal sealed class RssRule
{
    public string Selector = string.Empty;
    public string? ParentSelector;
    public RssPseudoState Pseudo;
    public readonly List<RssDeclaration> Declarations = [];
    public Location Location = Location.None;
}

internal sealed class RssFile
{
    public string FilePath = string.Empty;
    public readonly List<RssRule> Rules = [];
    public readonly List<RssVariable> Variables = [];
    public readonly List<Diagnostic> Diagnostics = [];
}

internal sealed class RssLexerResult
{
    public readonly List<RssToken> Tokens = [];
    public readonly List<Diagnostic> Diagnostics = [];
}
