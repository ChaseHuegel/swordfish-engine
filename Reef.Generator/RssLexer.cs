using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Reef.Generator;

internal sealed class RssLexer
{
    private readonly string _path;
    private readonly string _text;
    private readonly SourceText _sourceText;
    private readonly RssLexerResult _result = new();
    private int _offset;
    private int _line;
    private int _column;

    public RssLexer(string path, string text)
    {
        _path = path;
        _text = text;
        _sourceText = SourceText.From(text);
    }

    public RssLexerResult Lex()
    {
        while (_offset < _text.Length)
        {
            char c = _text[_offset];
            if (c == '/' && Peek(1) == '*')
            {
                LexComment();
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                LexWhitespace();
                continue;
            }

            switch (c)
            {
                case '.':
                    Add(RssTokenKind.Dot, 1);
                    break;
                case ':':
                    Add(RssTokenKind.Colon, 1);
                    break;
                case ',':
                    Add(RssTokenKind.Comma, 1);
                    break;
                case '(':
                    Add(RssTokenKind.LParen, 1);
                    break;
                case ')':
                    Add(RssTokenKind.RParen, 1);
                    break;
                case '{':
                    Add(RssTokenKind.LBrace, 1);
                    break;
                case '}':
                    Add(RssTokenKind.RBrace, 1);
                    break;
                case ';':
                    Add(RssTokenKind.Semicolon, 1);
                    break;
                case '|':
                    Add(RssTokenKind.Pipe, 1);
                    break;
                case '!':
                    Add(RssTokenKind.Bang, 1);
                    break;
                case '#':
                    LexHash();
                    break;
                case '\'':
                case '"':
                    LexString(c);
                    break;
                case '@':
                    AddError("The '@' character is not valid in a style sheet.", _offset, 1, _line, _column);
                    Advance(1);
                    break;
                default:
                    if (c == '-' && Peek(1) == '-')
                    {
                        LexVariable();
                    }
                    else if (char.IsDigit(c) || (c == '-' && char.IsDigit(Peek(1))) || (c == '.' && char.IsDigit(Peek(1))))
                    {
                        LexNumber();
                    }
                    else if (IsIdentifierStart(c))
                    {
                        LexIdentifier();
                    }
                    else
                    {
                        AddError(
                            "Unexpected character '" + c + "'.",
                            _offset,
                            1,
                            _line,
                            _column);
                        Advance(1);
                    }

                    break;
            }
        }

        _result.Tokens.Add(new RssToken(RssTokenKind.End, string.Empty, _offset, 0, _line, _column));
        return _result;
    }

    private void LexComment()
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;
        Advance(2);

        while (_offset < _text.Length)
        {
            if (_text[_offset] == '*' && Peek(1) == '/')
            {
                Advance(2);
                AddToken(RssTokenKind.Whitespace, " ", start, _offset - start, startLine, startColumn);
                return;
            }

            Advance(1);
        }

        AddError("Block comment is not terminated.", start, _offset - start, startLine, startColumn);
    }

    private void LexWhitespace()
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;
        while (_offset < _text.Length && char.IsWhiteSpace(_text[_offset]))
        {
            Advance(1);
        }

        AddToken(RssTokenKind.Whitespace, _text.Substring(start, _offset - start), start, _offset - start, startLine, startColumn);
    }

    private void LexHash()
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;
        Advance(1);
        while (_offset < _text.Length && IsHex(_text[_offset]))
        {
            Advance(1);
        }

        AddToken(RssTokenKind.Hash, _text.Substring(start, _offset - start), start, _offset - start, startLine, startColumn);
    }

    private void LexString(char quote)
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;
        Advance(1);
        var builder = new StringBuilder();

        while (_offset < _text.Length)
        {
            char c = _text[_offset];
            if (c == quote)
            {
                Advance(1);
                AddToken(RssTokenKind.String, builder.ToString(), start, _offset - start, startLine, startColumn);
                return;
            }

            if (c == '\\' && _offset + 1 < _text.Length)
            {
                builder.Append(_text[_offset + 1]);
                Advance(2);
                continue;
            }

            builder.Append(c);
            Advance(1);
        }

        AddError("String is not terminated.", start, _offset - start, startLine, startColumn);
    }

    private void LexVariable()
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;
        Advance(2);
        while (_offset < _text.Length && IsIdentifierPart(_text[_offset]))
        {
            Advance(1);
        }

        AddToken(RssTokenKind.Variable, _text.Substring(start, _offset - start), start, _offset - start, startLine, startColumn);
    }

    private void LexNumber()
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;

        if (_text[_offset] == '-')
        {
            Advance(1);
        }

        while (_offset < _text.Length && char.IsDigit(_text[_offset]))
        {
            Advance(1);
        }

        if (_offset < _text.Length && _text[_offset] == '.')
        {
            Advance(1);
            while (_offset < _text.Length && char.IsDigit(_text[_offset]))
            {
                Advance(1);
            }
        }

        while (_offset < _text.Length && (char.IsLetter(_text[_offset]) || _text[_offset] == '%'))
        {
            Advance(1);
        }

        AddToken(RssTokenKind.Number, _text.Substring(start, _offset - start), start, _offset - start, startLine, startColumn);
    }

    private void LexIdentifier()
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;
        Advance(1);
        while (_offset < _text.Length && IsIdentifierPart(_text[_offset]))
        {
            Advance(1);
        }

        AddToken(RssTokenKind.Identifier, _text.Substring(start, _offset - start), start, _offset - start, startLine, startColumn);
    }

    private void Add(RssTokenKind kind, int length)
    {
        int start = _offset;
        int startLine = _line;
        int startColumn = _column;
        Advance(length);
        AddToken(kind, _text.Substring(start, length), start, length, startLine, startColumn);
    }

    private void AddToken(RssTokenKind kind, string text, int offset, int length, int line, int column)
    {
        _result.Tokens.Add(new RssToken(kind, text, offset, length, line, column));
    }

    private void AddError(string message, int offset, int length, int line, int column)
    {
        Location location = DiagnosticDescriptors.CreateLocation(_path, _sourceText, offset, length, line, column);
        _result.Diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.StyleSyntax, location, message));
    }

    private void Advance(int count)
    {
        for (var i = 0; i < count && _offset < _text.Length; i++)
        {
            char c = _text[_offset];
            _offset++;
            if (c == '\n')
            {
                if (_offset < 2 || _text[_offset - 2] != '\r')
                {
                    _line++;
                }

                _column = 0;
            }
            else if (c == '\r')
            {
                _line++;
                _column = 0;
            }
            else
            {
                _column++;
            }
        }
    }

    private char Peek(int distance)
    {
        int index = _offset + distance;
        return index < _text.Length ? _text[index] : '\0';
    }

    private static bool IsHex(char c)
    {
        return (c >= '0' && c <= '9')
               || (c >= 'a' && c <= 'f')
               || (c >= 'A' && c <= 'F');
    }

    private static bool IsIdentifierStart(char c)
    {
        return char.IsLetter(c) || c == '_' || c == '-';
    }

    private static bool IsIdentifierPart(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_' || c == '-';
    }
}
