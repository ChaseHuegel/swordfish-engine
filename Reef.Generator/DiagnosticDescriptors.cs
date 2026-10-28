using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Reef.Generator;

internal static class DiagnosticDescriptors
{
    private const string CATEGORY = "Reef.Markup";

    public static readonly DiagnosticDescriptor XmlSyntax = Create(
        "RF0001",
        "Invalid Reef markup XML",
        "Reef markup XML is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor StyleSyntax = Create(
        "RF0002",
        "Invalid Reef style sheet",
        "Reef style sheet is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor StyleInheritance = Create(
        "RF0003",
        "Invalid style inheritance",
        "Style inheritance is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnresolvedExpression = Create(
        "RF0004",
        "Unresolved expression",
        "Cannot resolve expression: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidConstraint = Create(
        "RF0005",
        "Invalid constraint value",
        "Constraint value '{0}' is invalid: {1}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnusedContentSlot = Create(
        "RF0006",
        "Unused content slot",
        "Content slot '{0}' is not declared by component '{1}'",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor UnresolvedNamespace = Create(
        "RF0007",
        "Unresolved XML namespace",
        "XML namespace prefix '{0}' cannot be resolved: {1}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor ConditionalOrder = Create(
        "RF0008",
        "Invalid conditional order",
        "Conditional tag is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidEmpty = Create(
        "RF0009",
        "Invalid Empty tag",
        "Empty tag is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor LoopIdRequired = Create(
        "RF0010",
        "Loop interactive element needs an ID",
        "Interactive element inside a ForEach must declare a key-bound id. {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidBind = Create(
        "RF0011",
        "Invalid bind expression",
        "Bind expression is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor DuplicateStyleVariable = Create(
        "RF0012",
        "Duplicate style variable",
        "Style variable '{0}' is declared more than once without override",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor ConflictingStyleOverride = Create(
        "RF0013",
        "Conflicting style override",
        "Style variable '{0}' has more than one override declaration",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor WidgetChildren = Create(
        "RF0014",
        "Widget cannot contain children",
        "Widget '<{0}>' returns '{1}' and cannot contain child elements",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnknownElement = Create(
        "RF0015",
        "Unknown element or component",
        "Unknown element or component '<{0}>'",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidColor = Create(
        "RF0016",
        "Invalid color value",
        "Color value '{0}' is invalid",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidDimension = Create(
        "RF0017",
        "Invalid dimension value",
        "Dimension value '{0}' is invalid: {1}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidShorthand = Create(
        "RF0018",
        "Invalid shorthand value",
        "Shorthand value '{0}' is invalid: {1}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnknownAttribute = Create(
        "RF0019",
        "Unknown or invalid attribute",
        "Attribute '{0}' is not valid on '<{1}>': {2}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidExpression = Create(
        "RF0020",
        "Invalid expression",
        "Expression '{0}' is invalid: {1}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor TypeMismatch = Create(
        "RF0021",
        "Type mismatch",
        "Expression type mismatch: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnknownStyleProperty = Create(
        "RF0022",
        "Unknown style property",
        "Style property '{0}' is not supported",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidEnumValue = Create(
        "RF0023",
        "Invalid enum value",
        "Value '{0}' is not valid for '{1}'",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor DuplicateElementId = Create(
        "RF0024",
        "Duplicate element ID",
        "Element ID '{0}' is declared more than once in this document",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor CircularComponent = Create(
        "RF0025",
        "Circular component reference",
        "Component '{0}' creates a circular reference through '{1}'",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor MissingAttribute = Create(
        "RF0026",
        "Missing required attribute",
        "Tag '<{0}>' requires attribute '{1}'",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidClassDirective = Create(
        "RF0027",
        "Invalid class directive",
        "Class directive '{0}' is invalid: {1}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor InvalidRoot = Create(
        "RF0028",
        "Invalid root declaration",
        "Root declaration is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor DuplicateComponent = Create(
        "RF0029",
        "Duplicate component class",
        "Component class '{0}' is declared in more than one markup file",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor SlotMismatch = Create(
        "RF0030",
        "Component slot or context mismatch",
        "Component call is invalid: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnsupportedConstruct = Create(
        "RF0031",
        "Unsupported construct",
        "Construct is not supported: {0}",
        DiagnosticSeverity.Error);

    public static readonly DiagnosticDescriptor UnknownStyleClass = Create(
        "RF0032",
        "Unknown style class",
        "Style class '{0}' is not declared in any style sheet",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor ScrollConfiguration = Create(
        "RF0033",
        "Scroll configuration warning",
        "Element declares '{0}' without a scroll mode",
        DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor InternalError = Create(
        "RF0099",
        "Reef markup generator error",
        "The Reef markup generator failed: {0}",
        DiagnosticSeverity.Error);

    private static DiagnosticDescriptor Create(string id, string title, string message, DiagnosticSeverity severity)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            message,
            CATEGORY,
            severity,
            isEnabledByDefault: true);
    }

    public static Location CreateLocation(string path, SourceText text, int offset, int length, int line, int column)
    {
        if (offset < 0)
        {
            offset = 0;
        }

        if (offset > text.Length)
        {
            offset = text.Length;
        }

        if (length < 0)
        {
            length = 0;
        }

        if (offset + length > text.Length)
        {
            length = text.Length - offset;
        }

        var span = new TextSpan(offset, length);
        var linePosition = new LinePosition(line, column);
        var linePositionSpan = new LinePositionSpan(linePosition, linePosition);
        return Location.Create(path, span, linePositionSpan);
    }
}
