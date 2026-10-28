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

    public override string ToString()
    {
        return _builder.ToString();
    }
}
