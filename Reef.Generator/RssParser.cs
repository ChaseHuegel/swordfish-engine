using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Reef.Generator;

internal sealed class RssParser
{
    private const string ROOT_SELECTOR = "root";

    private readonly string _path;
    private readonly SourceText _sourceText;
    private readonly List<RssToken> _tokens;
    private readonly RssFile _file = new();
    private int _index;

    public RssParser(string path, string text)
    {
        _path = path;
        _sourceText = SourceText.From(text);
        var lexer = new RssLexer(path, text);
        RssLexerResult lexed = lexer.Lex();
        _tokens = lexed.Tokens;
        _file.FilePath = path;
        _file.Diagnostics.AddRange(lexed.Diagnostics);
    }

    public RssFile Parse()
    {
        while (true)
        {
            SkipTrivia();
            if (Current.Kind == RssTokenKind.End)
            {
                break;
            }

            if (Current.Kind == RssTokenKind.Colon)
            {
                if (IsRootSelector())
                {
                    ConsumeToken();
                    ConsumeToken();
                    ParseRootBlock();
                }
                else
                {
                    AddError("Only ':root' is valid at the top level.", Current);
                    RecoverTopLevel();
                }

                continue;
            }

            if (Current.Kind == RssTokenKind.Dot)
            {
                ParseRule();
                continue;
            }

            AddError("Expected ':root' or a class selector.", Current);
            RecoverTopLevel();
        }

        return _file;
    }

    private bool IsRootSelector()
    {
        int index = NextNonTriviaIndex(_index + 1);
        return index < _tokens.Count
               && _tokens[index].Kind == RssTokenKind.Identifier
               && string.Equals(_tokens[index].Text, ROOT_SELECTOR, StringComparison.OrdinalIgnoreCase);
    }

    private void ParseRootBlock()
    {
        SkipTrivia();
        if (!TryConsume(RssTokenKind.LBrace))
        {
            AddError("Expected '{' after ':root'.", Current);
            RecoverTopLevel();
            return;
        }

        while (true)
        {
            SkipTrivia();
            if (TryConsume(RssTokenKind.RBrace))
            {
                return;
            }

            if (Current.Kind == RssTokenKind.End)
            {
                AddError("Style block is not terminated.", Current);
                return;
            }

            bool isOverride = false;
            if (Current.Kind == RssTokenKind.Identifier
                && string.Equals(Current.Text, "override", StringComparison.OrdinalIgnoreCase))
            {
                isOverride = true;
                ConsumeToken();
                SkipTrivia();
            }

            if (Current.Kind != RssTokenKind.Variable)
            {
                AddError("Expected a style variable such as '--color'.", Current);
                RecoverDeclaration();
                continue;
            }

            RssToken variable = ConsumeToken();
            var declaration = new RssVariable
            {
                Name = variable.Text.StartsWith("--", StringComparison.Ordinal) ? variable.Text.Substring(2) : variable.Text,
                IsOverride = isOverride,
                Location = CreateLocation(variable),
            };

            SkipTrivia();
            if (!TryConsume(RssTokenKind.Colon))
            {
                AddError("Expected ':' after style variable.", Current);
                RecoverDeclaration();
                continue;
            }

            declaration.Value.AddRange(ReadValue());
            _file.Variables.Add(declaration);
            if (!TryConsume(RssTokenKind.Semicolon) && Current.Kind != RssTokenKind.RBrace && Current.Kind != RssTokenKind.End)
            {
                AddError("Expected ';' after style variable.", Current);
                RecoverDeclaration();
            }
        }
    }

    private void ParseRule()
    {
        RssToken dot = ConsumeToken();
        SkipTrivia();
        if (Current.Kind != RssTokenKind.Identifier)
        {
            AddError("Expected a class name after '.'.", Current);
            RecoverTopLevel();
            return;
        }

        RssToken selectorToken = ConsumeToken();
        var rule = new RssRule
        {
            Selector = selectorToken.Text,
            Location = CreateLocation(dot),
        };

        SkipTrivia();
        if (Current.Kind == RssTokenKind.Colon)
        {
            ConsumeToken();
            SkipTrivia();
            if (Current.Kind == RssTokenKind.Dot)
            {
                ConsumeToken();
                SkipTrivia();
                if (Current.Kind != RssTokenKind.Identifier)
                {
                    AddError("Expected a base class name after ': .'.", Current);
                    RecoverTopLevel();
                    return;
                }

                rule.ParentSelector = ConsumeToken().Text;
                SkipTrivia();
                if (Current.Kind == RssTokenKind.Colon)
                {
                    ConsumeToken();
                    SkipTrivia();
                    rule.Pseudo = ParsePseudoState();
                }
            }
            else
            {
                rule.Pseudo = ParsePseudoState();
            }
        }

        SkipTrivia();
        if (!TryConsume(RssTokenKind.LBrace))
        {
            AddError("Expected '{' after class selector.", Current);
            RecoverTopLevel();
            return;
        }

        while (true)
        {
            SkipTrivia();
            if (TryConsume(RssTokenKind.RBrace))
            {
                break;
            }

            if (Current.Kind == RssTokenKind.End)
            {
                AddError("Style block is not terminated.", Current);
                break;
            }

            if (Current.Kind != RssTokenKind.Identifier)
            {
                AddError("Expected a style property name.", Current);
                RecoverDeclaration();
                continue;
            }

            RssToken property = ConsumeToken();
            SkipTrivia();
            if (!TryConsume(RssTokenKind.Colon))
            {
                AddError("Expected ':' after style property.", Current);
                RecoverDeclaration();
                continue;
            }

            var declaration = new RssDeclaration
            {
                Name = property.Text,
                Location = CreateLocation(property),
            };
            declaration.Value.AddRange(ReadValue());
            rule.Declarations.Add(declaration);

            if (!TryConsume(RssTokenKind.Semicolon) && Current.Kind != RssTokenKind.RBrace && Current.Kind != RssTokenKind.End)
            {
                AddError("Expected ';' after style declaration.", Current);
                RecoverDeclaration();
            }
        }

        _file.Rules.Add(rule);
    }

    private RssPseudoState ParsePseudoState()
    {
        if (Current.Kind != RssTokenKind.Identifier)
        {
            AddError("Expected a pseudo state such as ':hover'.", Current);
            return RssPseudoState.None;
        }

        RssToken token = ConsumeToken();
        if (string.Equals(token.Text, "hover", StringComparison.OrdinalIgnoreCase))
        {
            return RssPseudoState.Hover;
        }

        if (string.Equals(token.Text, "active", StringComparison.OrdinalIgnoreCase))
        {
            return RssPseudoState.Active;
        }

        if (string.Equals(token.Text, "focus", StringComparison.OrdinalIgnoreCase))
        {
            return RssPseudoState.Focus;
        }

        if (string.Equals(token.Text, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return RssPseudoState.Disabled;
        }

        AddDiagnostic(
            DiagnosticDescriptors.InvalidEnumValue,
            CreateLocation(token),
            token.Text,
            "pseudo state");
        return RssPseudoState.None;
    }

    private List<RssToken> ReadValue()
    {
        var value = new List<RssToken>();
        while (Current.Kind != RssTokenKind.Semicolon
               && Current.Kind != RssTokenKind.RBrace
               && Current.Kind != RssTokenKind.End)
        {
            if (Current.Kind == RssTokenKind.LBrace)
            {
                AddError("Nested style blocks are not supported.", Current);
                RecoverDeclaration();
                break;
            }

            value.Add(Current);
            _index++;
        }

        while (value.Count > 0 && value[0].Kind == RssTokenKind.Whitespace)
        {
            value.RemoveAt(0);
        }

        while (value.Count > 0 && value[value.Count - 1].Kind == RssTokenKind.Whitespace)
        {
            value.RemoveAt(value.Count - 1);
        }

        if (value.Count == 0)
        {
            AddError("Style declaration requires a value.", Current);
        }

        return value;
    }

    private void RecoverTopLevel()
    {
        while (Current.Kind != RssTokenKind.End)
        {
            if (Current.Kind == RssTokenKind.RBrace)
            {
                ConsumeToken();
                return;
            }

            _index++;
        }
    }

    private void RecoverDeclaration()
    {
        while (Current.Kind != RssTokenKind.End && Current.Kind != RssTokenKind.Semicolon && Current.Kind != RssTokenKind.RBrace)
        {
            _index++;
        }

        if (Current.Kind == RssTokenKind.Semicolon)
        {
            ConsumeToken();
        }
    }

    private void SkipTrivia()
    {
        while (Current.Kind == RssTokenKind.Whitespace)
        {
            _index++;
        }
    }

    private bool TryConsume(RssTokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        _index++;
        return true;
    }

    private RssToken ConsumeToken()
    {
        RssToken token = Current;
        if (_index < _tokens.Count - 1)
        {
            _index++;
        }

        return token;
    }

    private RssToken Current => _index < _tokens.Count ? _tokens[_index] : _tokens[_tokens.Count - 1];

    private int NextNonTriviaIndex(int index)
    {
        while (index < _tokens.Count && _tokens[index].Kind == RssTokenKind.Whitespace)
        {
            index++;
        }

        return index;
    }

    private Location CreateLocation(RssToken token)
    {
        return DiagnosticDescriptors.CreateLocation(
            _path,
            _sourceText,
            token.Offset,
            token.Length,
            token.Line,
            token.Column);
    }

    private void AddError(string message, RssToken token)
    {
        AddDiagnostic(DiagnosticDescriptors.StyleSyntax, CreateLocation(token), message);
    }

    private void AddDiagnostic(DiagnosticDescriptor descriptor, Location location, params object[] args)
    {
        _file.Diagnostics.Add(Diagnostic.Create(descriptor, location, args));
    }
}
