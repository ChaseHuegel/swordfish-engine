using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Reef.Generator;

internal sealed class RmlParser
{
    private const string REEF_NAMESPACE = "Reef";
    private const string XML_NAMESPACE = "http://www.w3.org/XML/1998/namespace";
    private const string XSI_NAMESPACE = "http://www.w3.org/2001/XMLSchema-instance";
    private const string CLASS_NAMESPACE = "Reef.Class";

    private readonly AdditionalFileItem _file;
    private readonly SourceText _sourceText;
    private readonly RmlDocument _document;
    private IXmlLineInfo? _lineInfo;
    private XmlReader _reader = null!;
    private CancellationToken _cancellationToken;

    public RmlParser(AdditionalFileItem file)
    {
        _file = file;
        _sourceText = file.SourceText;
        _document = new RmlDocument
        {
            FilePath = file.Path,
            Location = CreateLocation(1, 1, 0),
        };
    }

    public RmlDocument Parse(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            CheckCharacters = true,
            CloseInput = true,
        };

        try
        {
            using var textReader = new StringReader(_file.Text);
            using var reader = XmlReader.Create(textReader, settings);
            _reader = reader;
            _lineInfo = reader as IXmlLineInfo;

            if (reader.MoveToContent() == XmlNodeType.None || reader.NodeType != XmlNodeType.Element)
            {
                AddDiagnostic(DiagnosticDescriptors.XmlSyntax, CreateLocation(reader), "No root element was found.");
                return _document;
            }

            if (!string.Equals(reader.LocalName, "Reef", StringComparison.Ordinal))
            {
                AddDiagnostic(
                    DiagnosticDescriptors.XmlSyntax,
                    CreateLocation(reader),
                    "The root element must be <Reef>.");
                return _document;
            }

            if (reader.NamespaceURI.Length > 0 && !string.Equals(reader.NamespaceURI, REEF_NAMESPACE, StringComparison.Ordinal))
            {
                AddDiagnostic(
                    DiagnosticDescriptors.XmlSyntax,
                    CreateLocation(reader),
                    "The root namespace must be 'Reef'.");
                return _document;
            }

            _document.DefaultNamespace = reader.NamespaceURI;
            _document.Location = CreateLocation(reader);
            ReadRootAttributes();

            _document.Roots.AddRange(ParseChildren());
            CleanupEmptyNodes(_document.Roots);

            if (reader.MoveToContent() != XmlNodeType.None && reader.NodeType != XmlNodeType.EndElement)
            {
                AddDiagnostic(
                    DiagnosticDescriptors.XmlSyntax,
                    CreateLocation(reader),
                    "Only one root element is allowed.");
            }
        }
        catch (XmlException exception)
        {
            var location = CreateLocation(exception.LineNumber, exception.LinePosition, 0);
            string message = exception.Message;
            if (message.IndexOf("prefix", StringComparison.OrdinalIgnoreCase) >= 0
                && message.IndexOf("not defined", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddDiagnostic(DiagnosticDescriptors.UnresolvedNamespace, location, ExtractPrefix(message), message);
            }
            else
            {
                AddDiagnostic(DiagnosticDescriptors.XmlSyntax, location, message);
            }
        }
        catch (Exception exception)
        {
            AddDiagnostic(DiagnosticDescriptors.InternalError, CreateLocation(1, 1, 0), exception.Message);
        }

        return _document;
    }

    private void ReadRootAttributes()
    {
        if (_reader.HasAttributes)
        {
            _reader.MoveToFirstAttribute();
            do
            {
                if (_reader.Prefix == "xmlns" || (_reader.Prefix.Length == 0 && _reader.LocalName == "xmlns"))
                {
                    continue;
                }

                if (_reader.NamespaceURI.Length > 0 && _reader.NamespaceURI != XML_NAMESPACE && _reader.NamespaceURI != XSI_NAMESPACE)
                {
                    continue;
                }

                string name = _reader.LocalName;
                string value = _reader.Value;
                if (string.Equals(name, "Class", StringComparison.Ordinal))
                {
                    _document.ClassName = value.Trim();
                    _document.ClassLocation = CreateLocation(CurrentLine, CurrentColumn, value.Length);
                }
                else if (string.Equals(name, "Access", StringComparison.Ordinal))
                {
                    _document.Access = value.Trim();
                }
                else if (string.Equals(name, "Context", StringComparison.Ordinal))
                {
                    _document.ContextType = value.Trim();
                }
                else if (string.Equals(name, "Parameters", StringComparison.Ordinal))
                {
                    _document.ParameterType = value.Trim();
                }
                else if (string.Equals(name, "RendererData", StringComparison.Ordinal))
                {
                    _document.RendererDataType = value.Trim();
                }
                else if (string.Equals(name, "Namespace", StringComparison.Ordinal))
                {
                    _document.NamespaceName = value.Trim();
                }
                else
                {
                    AddDiagnostic(
                        DiagnosticDescriptors.UnknownAttribute,
                        CreateLocation(CurrentLine, CurrentColumn, name.Length),
                        name,
                        "Reef",
                        "The root attribute is not supported.");
                }
            }
            while (_reader.MoveToNextAttribute());

            _reader.MoveToElement();
        }

        if (_document.ClassName.Length == 0)
        {
            AddDiagnostic(
                DiagnosticDescriptors.MissingAttribute,
                CreateLocation(_reader),
                "Reef",
                "Class");
        }
    }

    private List<RmlNode> ParseChildren()
    {
        var nodes = new List<RmlNode>();

        while (!_cancellationToken.IsCancellationRequested && _reader.Read())
        {
            switch (_reader.NodeType)
            {
                case XmlNodeType.Element:
                    nodes.Add(ParseElement());
                    break;
                case XmlNodeType.EndElement:
                    GroupNodes(nodes);
                    return nodes;
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                case XmlNodeType.Comment:
                case XmlNodeType.ProcessingInstruction:
                    break;
            }
        }

        GroupNodes(nodes);
        return nodes;
    }

    private RmlNode ParseElement()
    {
        var element = new RmlElementNode
        {
            Tag = _reader.LocalName,
            Prefix = _reader.Prefix,
            NamespaceUri = _reader.NamespaceURI,
            Line = CurrentLine,
            Column = CurrentColumn,
            Location = CreateLocation(CurrentLine, CurrentColumn, _reader.LocalName.Length),
        };

        if (element.Prefix.Length > 0
            && element.NamespaceUri.Length > 0
            && element.NamespaceUri != REEF_NAMESPACE
            && element.NamespaceUri != CLASS_NAMESPACE
            && element.NamespaceUri != XML_NAMESPACE
            && element.NamespaceUri != XSI_NAMESPACE)
        {
            _document.NamespacePrefixes[element.Prefix] = element.NamespaceUri;
        }

        ReadElementAttributes(element);

        if (_reader.IsEmptyElement)
        {
            return ConvertElement(element);
        }

        if (string.Equals(element.Tag, "Text", StringComparison.Ordinal)
            && (element.NamespaceUri.Length == 0 || element.NamespaceUri == REEF_NAMESPACE))
        {
            ParseTextBody(element);
            return element;
        }

        element.Children.AddRange(ParseChildren());
        return ConvertElement(element);
    }

    private void ValidateControlAttributes(RmlElementNode element, params string[] allowed)
    {
        for (var i = 0; i < element.Attributes.Count; i++)
        {
            RmlAttribute attribute = element.Attributes[i];
            if (attribute.NamespaceUri == XML_NAMESPACE
                || attribute.NamespaceUri == XSI_NAMESPACE
                || attribute.IsClassDirective)
            {
                continue;
            }

            if (attribute.NamespaceUri.Length > 0 || System.Array.IndexOf(allowed, attribute.Name) < 0)
            {
                AddDiagnostic(
                    DiagnosticDescriptors.UnknownAttribute,
                    attribute.Location,
                    attribute.QualifiedName,
                    element.Tag,
                    "The attribute is not valid on this control tag.");
            }
        }
    }

    private RmlNode ConvertElement(RmlElementNode element)
    {
        if (element.Prefix.Length > 0 || (element.NamespaceUri.Length > 0 && element.NamespaceUri != REEF_NAMESPACE))
        {
            return element;
        }

        switch (element.Tag)
        {
            case "If":
                if (element.GetAttributeText("condition") == null)
                {
                    AddDiagnostic(DiagnosticDescriptors.MissingAttribute, element.Location, "If", "condition");
                }

                ValidateControlAttributes(element, "condition");
                var ifNode = new RmlIfNode
                {
                    Condition = element.GetAttributeText("condition"),
                    Location = element.Location,
                    Line = element.Line,
                    Column = element.Column,
                };
                ifNode.Children.AddRange(element.Children);
                return ifNode;
            case "ElseIf":
                if (element.GetAttributeText("condition") == null)
                {
                    AddDiagnostic(DiagnosticDescriptors.MissingAttribute, element.Location, "ElseIf", "condition");
                }

                ValidateControlAttributes(element, "condition");
                var elseIf = new RmlElseIfNode
                {
                    Condition = element.GetAttributeText("condition"),
                    Location = element.Location,
                    Line = element.Line,
                    Column = element.Column,
                };
                elseIf.Children.AddRange(element.Children);
                return elseIf;
            case "Else":
                ValidateControlAttributes(element);
                var elseNode = new RmlElseNode
                {
                    Location = element.Location,
                    Line = element.Line,
                    Column = element.Column,
                };
                elseNode.Children.AddRange(element.Children);
                return elseNode;
            case "ForEach":
                ValidateControlAttributes(element, "in", "as", "index");
                var forEach = new RmlForEachNode
                {
                    InExpression = element.GetAttributeText("in"),
                    AsName = element.GetAttributeText("as"),
                    IndexName = element.GetAttributeText("index"),
                    Location = element.Location,
                    Line = element.Line,
                    Column = element.Column,
                };
                forEach.Children.AddRange(element.Children);
                return forEach;
            case "Content":
                ValidateControlAttributes(element, "name");
                var content = new RmlContentNode
                {
                    Name = element.GetAttributeText("name"),
                    Location = element.Location,
                    Line = element.Line,
                    Column = element.Column,
                };
                content.Children.AddRange(element.Children);
                return content;
            case "Empty":
                ValidateControlAttributes(element);
                var empty = new RmlEmptyNode
                {
                    Location = element.Location,
                    Line = element.Line,
                    Column = element.Column,
                };
                empty.Children.AddRange(element.Children);
                return empty;
            default:
                return element;
        }
    }

    private void ReadElementAttributes(RmlElementNode element)
    {
        if (!_reader.HasAttributes)
        {
            return;
        }

        _reader.MoveToFirstAttribute();
        do
        {
            if (_reader.Prefix == "xmlns" || (_reader.Prefix.Length == 0 && _reader.LocalName == "xmlns"))
            {
                continue;
            }

            var attribute = new RmlAttribute
            {
                Name = _reader.LocalName,
                Prefix = _reader.Prefix,
                NamespaceUri = _reader.NamespaceURI,
                Value = _reader.Value,
                Order = element.Attributes.Count,
                Line = CurrentLine,
                Column = CurrentColumn,
                Location = CreateLocation(CurrentLine, CurrentColumn, _reader.LocalName.Length),
            };

            if (attribute.NamespaceUri == CLASS_NAMESPACE)
            {
                _document.HasClassNamespace = true;
            }

            element.Attributes.Add(attribute);
        }
        while (_reader.MoveToNextAttribute());

        _reader.MoveToElement();
    }

    private void ParseTextBody(RmlElementNode element)
    {
        var builder = new StringBuilder();
        RmlAttribute? space = element.FindXmlAttribute("space");
        element.PreserveWhitespace = space != null
                                     && string.Equals(space.Value, "preserve", StringComparison.OrdinalIgnoreCase);

        while (!_cancellationToken.IsCancellationRequested && _reader.Read())
        {
            switch (_reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    builder.Append(_reader.Value);
                    break;
                case XmlNodeType.Element:
                    element.HasElementChildren = true;
                    AddDiagnostic(
                        DiagnosticDescriptors.UnsupportedConstruct,
                        CreateLocation(_reader),
                        "Text elements cannot contain child elements. Use the text attribute or separate Text elements.");
                    _reader.Skip();
                    break;
                case XmlNodeType.EndElement:
                    element.TextContent = builder.ToString();
                    return;
                case XmlNodeType.Comment:
                case XmlNodeType.ProcessingInstruction:
                    break;
            }
        }

        element.TextContent = builder.ToString();
    }

    private void CleanupEmptyNodes(List<RmlNode> nodes)
    {
        RemoveMisplacedEmpty(nodes);

        for (var i = 0; i < nodes.Count; i++)
        {
            switch (nodes[i])
            {
                case RmlElementNode element:
                    CleanupEmptyNodes(element.Children);
                    break;
                case RmlConditionalNode conditional:
                    for (var j = 0; j < conditional.Branches.Count; j++)
                    {
                        CleanupEmptyNodes(conditional.Branches[j].Children);
                    }

                    break;
                case RmlForEachNode forEach:
                    CleanupEmptyNodes(forEach.Children);
                    CleanupEmptyNodes(forEach.EmptyChildren);
                    break;
                case RmlContentNode content:
                    CleanupEmptyNodes(content.Children);
                    break;
            }
        }
    }

    private void GroupNodes(List<RmlNode> nodes)
    {
        GroupConditionals(nodes);
        GroupEmptyNodes(nodes);

        for (var i = 0; i < nodes.Count; i++)
        {
            GroupNodeChildren(nodes[i]);
        }
    }

    private void GroupNodeChildren(RmlNode node)
    {
        switch (node)
        {
            case RmlElementNode element:
                GroupNodes(element.Children);
                break;
            case RmlConditionalNode conditional:
                for (var i = 0; i < conditional.Branches.Count; i++)
                {
                    GroupNodes(conditional.Branches[i].Children);
                }

                break;
            case RmlForEachNode forEach:
                GroupNodes(forEach.Children);
                GroupNodes(forEach.EmptyChildren);
                break;
            case RmlContentNode content:
                GroupNodes(content.Children);
                break;
            case RmlEmptyNode empty:
                GroupNodes(empty.Children);
                break;
        }
    }

    private void GroupConditionals(List<RmlNode> nodes)
    {
        var grouped = new List<RmlNode>(nodes.Count);

        for (var i = 0; i < nodes.Count; i++)
        {
            RmlNode node = nodes[i];
            if (node is RmlIfNode ifNode)
            {
                var conditional = new RmlConditionalNode
                {
                    Location = ifNode.Location,
                    Line = ifNode.Line,
                    Column = ifNode.Column,
                };

                var firstBranch = new RmlConditionalBranch
                {
                    Condition = ifNode.Condition,
                    Location = ifNode.Location,
                };
                firstBranch.Children.AddRange(ifNode.Children);
                conditional.Branches.Add(firstBranch);

                var sawElse = false;
                var j = i + 1;
                while (j < nodes.Count && (nodes[j] is RmlElseIfNode || nodes[j] is RmlElseNode))
                {
                    if (nodes[j] is RmlElseIfNode elseIf)
                    {
                        if (sawElse)
                        {
                            AddDiagnostic(
                                DiagnosticDescriptors.ConditionalOrder,
                                elseIf.Location,
                                "<ElseIf> cannot appear after <Else>.");
                        }

                        var branch = new RmlConditionalBranch
                        {
                            Condition = elseIf.Condition,
                            Location = elseIf.Location,
                        };
                        branch.Children.AddRange(elseIf.Children);
                        conditional.Branches.Add(branch);
                    }
                    else if (nodes[j] is RmlElseNode elseBranchNode)
                    {
                        if (sawElse)
                        {
                            AddDiagnostic(
                                DiagnosticDescriptors.ConditionalOrder,
                                elseBranchNode.Location,
                                "A conditional chain can contain only one <Else>.");
                        }

                        sawElse = true;
                        var branch = new RmlConditionalBranch
                        {
                            Condition = null,
                            Location = elseBranchNode.Location,
                        };
                        branch.Children.AddRange(elseBranchNode.Children);
                        conditional.Branches.Add(branch);
                    }

                    j++;
                }

                grouped.Add(conditional);
                i = j - 1;
                continue;
            }

            if (node is RmlElseIfNode elseIfNode)
            {
                AddDiagnostic(
                    DiagnosticDescriptors.ConditionalOrder,
                    elseIfNode.Location,
                    "<ElseIf> must follow an <If>.");
                continue;
            }

            if (node is RmlElseNode elseNode)
            {
                AddDiagnostic(
                    DiagnosticDescriptors.ConditionalOrder,
                    elseNode.Location,
                    "<Else> must follow an <If>.");
                continue;
            }

            grouped.Add(node);
        }

        nodes.Clear();
        nodes.AddRange(grouped);
    }

    private void GroupEmptyNodes(List<RmlNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is RmlForEachNode forEach)
            {
                var filtered = new List<RmlNode>(forEach.Children.Count);
                for (var j = 0; j < forEach.Children.Count; j++)
                {
                    RmlNode child = forEach.Children[j];
                    if (child is RmlEmptyNode empty)
                    {
                        if (forEach.EmptyLocation != Location.None)
                        {
                            AddDiagnostic(
                                DiagnosticDescriptors.InvalidEmpty,
                                empty.Location,
                                "A <ForEach> can contain only one <Empty> tag.");
                            continue;
                        }

                        forEach.EmptyLocation = empty.Location;
                        forEach.EmptyChildren.AddRange(empty.Children);
                        continue;
                    }

                    filtered.Add(child);
                }

                forEach.Children.Clear();
                forEach.Children.AddRange(filtered);
            }
        }

    }

    private void RemoveMisplacedEmpty(List<RmlNode> nodes)
    {
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i] is RmlEmptyNode empty)
            {
                AddDiagnostic(
                    DiagnosticDescriptors.InvalidEmpty,
                    empty.Location,
                    "<Empty> must be a direct child of <ForEach>.");
                nodes.RemoveAt(i);
            }
        }
    }

    private int CurrentLine => _lineInfo?.LineNumber ?? 0;

    private int CurrentColumn => _lineInfo?.LinePosition ?? 0;

    private Location CreateLocation(XmlReader reader)
    {
        return CreateLocation(CurrentLine, CurrentColumn, 0);
    }

    private Location CreateLocation(int line, int column, int length)
    {
        int lineIndex = Math.Max(0, Math.Min(line - 1, _sourceText.Lines.Count - 1));
        TextLine sourceLine = _sourceText.Lines[lineIndex];
        int offset = Math.Max(0, Math.Min(sourceLine.Start + Math.Max(0, column - 1), _sourceText.Length));
        return DiagnosticDescriptors.CreateLocation(
            _file.Path,
            _sourceText,
            offset,
            length,
            lineIndex,
            Math.Max(0, column - 1));
    }

    private void AddDiagnostic(DiagnosticDescriptor descriptor, Location location, params object[] args)
    {
        _document.Diagnostics.Add(Diagnostic.Create(descriptor, location, args));
    }

    private static string ExtractPrefix(string message)
    {
        int first = message.IndexOf('\'');
        if (first < 0)
        {
            return string.Empty;
        }

        int second = message.IndexOf('\'', first + 1);
        if (second <= first)
        {
            return string.Empty;
        }

        return message.Substring(first + 1, second - first - 1);
    }
}
