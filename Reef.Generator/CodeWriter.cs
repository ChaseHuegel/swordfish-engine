using System;
using System.Text;

namespace Reef.Generator;

internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Line(string text = "")
    {
        if (text.Length > 0)
        {
            for (var i = 0; i < _indent; i++)
            {
                _builder.Append("    ");
            }

            _builder.Append(text);
        }

        _builder.Append('\n');
    }

    public void RawLine(string text)
    {
        _builder.Append(text);
        _builder.Append('\n');
    }

    public void Open()
    {
        Line("{");
        _indent++;
    }

    public void Close()
    {
        if (_indent > 0)
        {
            _indent--;
        }

        Line("}");
    }

    public void Indent()
    {
        _indent++;
    }

    public void Unindent()
    {
        if (_indent > 0)
        {
            _indent--;
        }
    }

    public string IndentText()
    {
        var text = new StringBuilder();
        for (var i = 0; i < _indent; i++)
        {
            text.Append("    ");
        }

        return text.ToString();
    }

    /// <summary>Marks the current position so <see cref="RenameParamFrom"/> can rewrite the text after it.</summary>
    public int Mark()
    {
        return _builder.Length;
    }

    /// <summary>Renames every whole-word <c>param</c> identifier written after the mark.</summary>
    public void RenameParamFrom(int mark, string name)
    {
        string segment = _builder.ToString(mark, _builder.Length - mark);
        _builder.Length = mark;
        _builder.Append(RenameIdentifier(segment, "param", name));
    }

    private static string RenameIdentifier(string text, string from, string to)
    {
        var result = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '"')
            {
                int start = i++;
                while (i < text.Length)
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i += 2;
                        continue;
                    }

                    if (text[i] == '"')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                result.Append(text, start, i - start);
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                int end = text.IndexOf('\n', i);
                if (end < 0)
                {
                    end = text.Length;
                }

                result.Append(text, i, end - i);
                i = end;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }

                string token = text.Substring(start, i - start);
                result.Append(string.Equals(token, from, StringComparison.Ordinal) ? to : token);
                continue;
            }

            result.Append(c);
            i++;
        }

        return result.ToString();
    }

    public override string ToString()
    {
        return _builder.ToString();
    }
}
