using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal static class StyleValueParser
{
    public static List<RssToken> TokenizeValue(string value)
    {
        var lexer = new RssLexer("<value>", value ?? string.Empty);
        RssLexerResult result = lexer.Lex();
        var tokens = new List<RssToken>();
        for (var i = 0; i < result.Tokens.Count; i++)
        {
            if (result.Tokens[i].Kind != RssTokenKind.End)
            {
                tokens.Add(result.Tokens[i]);
            }
        }

        return tokens;
    }

    public static bool TryParseInline(
        RmlElementNode element,
        RmlAttribute attribute,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        string name = attribute.Name.ToLowerInvariant();
        string trimmed = attribute.Value.TrimStart();

        if (trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            return TryParseExpressionAttribute(element, name, trimmed, attribute.Location, out assignments, out error);
        }

        List<RssToken> tokens = TokenizeValue(attribute.Value);
        if (tokens.Count == 0)
        {
            error = "No value was provided.";
            return false;
        }

        return TryParseInlineTokens(element, name, tokens, attribute.Location, out assignments, out error);
    }

    public static bool TryParseInlineTokens(
        RmlElementNode element,
        string name,
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        name = name.ToLowerInvariant();

        switch (name)
        {
            case "color":
                return AddColor(tokens, MarkupProperty.Color, location, out assignments, out error);
            case "background":
                return AddColor(tokens, MarkupProperty.BackgroundColor, location, out assignments, out error);
            case "padding":
                return AddPadding(tokens, location, out assignments, out error);
            case "corner-radius":
                return AddCornerRadius(tokens, location, out assignments, out error);
            case "border":
                return AddBorder(tokens, location, out assignments, out error);
            case "border-width":
                return AddInt(tokens, MarkupProperty.BorderThickness, location, out assignments, out error);
            case "border-color":
                return AddColor(tokens, MarkupProperty.BorderColor, location, out assignments, out error);
            case "corner-caps":
                return AddCornerCaps(tokens, location, out assignments, out error);
            case "corner-caps-length":
                return AddInt(tokens, MarkupProperty.CornerCapsLength, location, out assignments, out error);
            case "corner-caps-thickness":
                return AddInt(tokens, MarkupProperty.CornerCapsThickness, location, out assignments, out error);
            case "corner-caps-color":
                return AddColor(tokens, MarkupProperty.CornerCapsColor, location, out assignments, out error);
            case "overlay":
                return AddBool(tokens, MarkupProperty.Overlay, location, out assignments, out error);
            case "width":
                return AddConstraint(tokens, MarkupProperty.Width, location, out assignments, out error);
            case "height":
                return AddConstraint(tokens, MarkupProperty.Height, location, out assignments, out error);
            case "size":
                if (string.Equals(element.Tag, "Text", StringComparison.Ordinal))
                {
                    return AddInt(tokens, MarkupProperty.FontSize, location, out assignments, out error);
                }

                return AddSize(tokens, location, out assignments, out error);
            case "min-width":
                return AddInt(tokens, MarkupProperty.MinWidth, location, out assignments, out error);
            case "min-height":
                return AddInt(tokens, MarkupProperty.MinHeight, location, out assignments, out error);
            case "min-size":
                return AddMinMaxSize(tokens, isMin: true, location, out assignments, out error);
            case "max-width":
                return AddInt(tokens, MarkupProperty.MaxWidth, location, out assignments, out error);
            case "max-height":
                return AddInt(tokens, MarkupProperty.MaxHeight, location, out assignments, out error);
            case "max-size":
                return AddMinMaxSize(tokens, isMin: false, location, out assignments, out error);
            case "x":
                return AddConstraint(tokens, MarkupProperty.X, location, out assignments, out error);
            case "y":
                return AddConstraint(tokens, MarkupProperty.Y, location, out assignments, out error);
            case "pos":
            case "position":
                return AddPosition(tokens, location, out assignments, out error);
            case "anchor":
            case "anchors":
                return AddAnchors(tokens, location, out assignments, out error);
            case "layout":
                return AddLayout(tokens, location, out assignments, out error);
            case "spacing":
                return AddInt(tokens, MarkupProperty.Spacing, location, out assignments, out error);
            case "font":
                return AddFontId(tokens, location, out assignments, out error);
            case "font-size":
                return AddInt(tokens, MarkupProperty.FontSize, location, out assignments, out error);
            case "text-align":
            case "align":
                return AddHorizontalAlignment(tokens, location, out assignments, out error);
            case "vertical-align":
            case "valign":
                return AddVerticalAlignment(tokens, location, out assignments, out error);
            case "wrap":
                return AddWrap(tokens, location, out assignments, out error);
            default:
                error = "Attribute is not a style attribute.";
                return false;
        }
    }

    public static bool TryParseStylesheet(
        string propertyName,
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        string name = propertyName.ToLowerInvariant();

        switch (name)
        {
            case "color":
                return AddColor(tokens, MarkupProperty.Color, location, out assignments, out error);
            case "background":
                return AddColor(tokens, MarkupProperty.BackgroundColor, location, out assignments, out error);
            case "padding":
                return AddPadding(tokens, location, out assignments, out error);
            case "corner-radius":
                return AddCornerRadius(tokens, location, out assignments, out error);
            case "border":
                return AddBorder(tokens, location, out assignments, out error);
            case "border-width":
                return AddInt(tokens, MarkupProperty.BorderThickness, location, out assignments, out error);
            case "border-color":
                return AddColor(tokens, MarkupProperty.BorderColor, location, out assignments, out error);
            case "corner-caps":
                return AddCornerCaps(tokens, location, out assignments, out error);
            case "corner-caps-length":
                return AddInt(tokens, MarkupProperty.CornerCapsLength, location, out assignments, out error);
            case "corner-caps-thickness":
                return AddInt(tokens, MarkupProperty.CornerCapsThickness, location, out assignments, out error);
            case "corner-caps-color":
                return AddColor(tokens, MarkupProperty.CornerCapsColor, location, out assignments, out error);
            case "overlay":
                return AddBool(tokens, MarkupProperty.Overlay, location, out assignments, out error);
            case "font-size":
                return AddInt(tokens, MarkupProperty.FontSize, location, out assignments, out error);
            case "font":
            case "font-family":
                return AddFontId(tokens, location, out assignments, out error);
            case "text-align":
                return AddHorizontalAlignment(tokens, location, out assignments, out error);
            case "vertical-align":
                return AddVerticalAlignment(tokens, location, out assignments, out error);
            case "white-space":
                return AddWhiteSpace(tokens, location, out assignments, out error);
            case "width":
                return AddConstraint(tokens, MarkupProperty.Width, location, out assignments, out error);
            case "height":
                return AddConstraint(tokens, MarkupProperty.Height, location, out assignments, out error);
            case "size":
                return AddSize(tokens, location, out assignments, out error);
            case "min-width":
                return AddInt(tokens, MarkupProperty.MinWidth, location, out assignments, out error);
            case "min-height":
                return AddInt(tokens, MarkupProperty.MinHeight, location, out assignments, out error);
            case "min-size":
                return AddMinMaxSize(tokens, isMin: true, location, out assignments, out error);
            case "max-width":
                return AddInt(tokens, MarkupProperty.MaxWidth, location, out assignments, out error);
            case "max-height":
                return AddInt(tokens, MarkupProperty.MaxHeight, location, out assignments, out error);
            case "max-size":
                return AddMinMaxSize(tokens, isMin: false, location, out assignments, out error);
            case "x":
                return AddConstraint(tokens, MarkupProperty.X, location, out assignments, out error);
            case "y":
                return AddConstraint(tokens, MarkupProperty.Y, location, out assignments, out error);
            case "pos":
            case "position":
                return AddPosition(tokens, location, out assignments, out error);
            case "anchor":
            case "anchors":
                return AddAnchors(tokens, location, out assignments, out error);
            case "layout":
                return AddLayout(tokens, location, out assignments, out error);
            case "spacing":
                return AddInt(tokens, MarkupProperty.Spacing, location, out assignments, out error);
            default:
                error = "Style property is not supported.";
                return false;
        }
    }

    private static bool TryParseExpressionAttribute(
        RmlElementNode element,
        string name,
        string raw,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        switch (name)
        {
            case "color":
                assignments.Add(new MarkupAssignment(MarkupProperty.Color, new MarkupExpression(raw, MarkupExpressionKind.Color), location));
                return true;
            case "background":
                assignments.Add(new MarkupAssignment(MarkupProperty.BackgroundColor, new MarkupExpression(raw, MarkupExpressionKind.Color), location));
                return true;
            case "border-color":
                assignments.Add(new MarkupAssignment(MarkupProperty.BorderColor, new MarkupExpression(raw, MarkupExpressionKind.Color), location));
                return true;
            case "corner-caps-color":
                assignments.Add(new MarkupAssignment(MarkupProperty.CornerCapsColor, new MarkupExpression(raw, MarkupExpressionKind.Color), location));
                return true;
            case "overlay":
                assignments.Add(new MarkupAssignment(MarkupProperty.Overlay, new MarkupExpression(raw, MarkupExpressionKind.Bool), location));
                return true;
            case "width":
                assignments.Add(new MarkupAssignment(MarkupProperty.Width, new MarkupExpression(raw, MarkupExpressionKind.Constraint), location));
                return true;
            case "height":
                assignments.Add(new MarkupAssignment(MarkupProperty.Height, new MarkupExpression(raw, MarkupExpressionKind.Constraint), location));
                return true;
            case "x":
                assignments.Add(new MarkupAssignment(MarkupProperty.X, new MarkupExpression(raw, MarkupExpressionKind.Constraint), location));
                return true;
            case "y":
                assignments.Add(new MarkupAssignment(MarkupProperty.Y, new MarkupExpression(raw, MarkupExpressionKind.Constraint), location));
                return true;
            case "min-width":
            case "min-height":
            case "max-width":
            case "max-height":
            case "spacing":
            case "font-size":
                assignments.Add(new MarkupAssignment(PropertyForName(name), new MarkupExpression(raw, MarkupExpressionKind.Int), location));
                return true;
            case "font":
                assignments.Add(new MarkupAssignment(MarkupProperty.FontID, new MarkupExpression(raw, MarkupExpressionKind.FontId), location));
                return true;
            case "size":
                if (string.Equals(element.Tag, "Text", StringComparison.Ordinal))
                {
                    assignments.Add(new MarkupAssignment(MarkupProperty.FontSize, new MarkupExpression(raw, MarkupExpressionKind.Int), location));
                    return true;
                }

                error = "The size shorthand does not support expressions. Use width and height.";
                return false;
            case "pos":
            case "position":
            case "padding":
            case "corner-radius":
            case "border":
            case "border-width":
            case "corner-caps":
            case "corner-caps-length":
            case "corner-caps-thickness":
            case "anchor":
            case "anchors":
            case "layout":
            case "text-align":
            case "align":
            case "vertical-align":
            case "valign":
            case "wrap":
            case "min-size":
            case "max-size":
                error = "This style attribute does not support expressions.";
                return false;
            default:
                error = "Attribute is not a style attribute.";
                return false;
        }
    }

    private static MarkupProperty PropertyForName(string name)
    {
        switch (name)
        {
            case "min-width":
                return MarkupProperty.MinWidth;
            case "min-height":
                return MarkupProperty.MinHeight;
            case "max-width":
                return MarkupProperty.MaxWidth;
            case "max-height":
                return MarkupProperty.MaxHeight;
            case "spacing":
                return MarkupProperty.Spacing;
            default:
                return MarkupProperty.FontSize;
        }
    }

    private static bool AddColor(
        IReadOnlyList<RssToken> tokens,
        MarkupProperty property,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        if (!ColorParser.TryParse(tokens, out Vector4 color, out error))
        {
            return false;
        }

        assignments.Add(new MarkupAssignment(property, color, location));
        return true;
    }

    private static bool AddInt(
        IReadOnlyList<RssToken> tokens,
        MarkupProperty property,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || values[0].Kind != RssTokenKind.Number)
        {
            error = "Expected one integer.";
            return false;
        }

        if (!TryParseInt(values[0].Text, out int value))
        {
            error = "Expected an integer.";
            return false;
        }

        assignments.Add(new MarkupAssignment(property, value, location));
        return true;
    }

    private static bool AddBool(
        IReadOnlyList<RssToken> tokens,
        MarkupProperty property,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || values[0].Kind != RssTokenKind.Identifier)
        {
            error = "Expected true or false.";
            return false;
        }

        if (string.Equals(values[0].Text, "true", StringComparison.OrdinalIgnoreCase))
        {
            assignments.Add(new MarkupAssignment(property, true, location));
            return true;
        }

        if (string.Equals(values[0].Text, "false", StringComparison.OrdinalIgnoreCase))
        {
            assignments.Add(new MarkupAssignment(property, false, location));
            return true;
        }

        error = "Expected true or false.";
        return false;
    }

    private static bool AddWrap(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        if (!AddBool(tokens, MarkupProperty.NoWrap, location, out List<MarkupAssignment> boolAssignments, out error))
        {
            return false;
        }

        MarkupAssignment assignment = boolAssignments[0];
        assignments.Add(new MarkupAssignment(MarkupProperty.NoWrap, !(bool)assignment.Value, location));
        return true;
    }

    private static bool AddWhiteSpace(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || values[0].Kind != RssTokenKind.Identifier)
        {
            error = "Expected 'normal' or 'nowrap'.";
            return false;
        }

        if (string.Equals(values[0].Text, "nowrap", StringComparison.OrdinalIgnoreCase))
        {
            assignments.Add(new MarkupAssignment(MarkupProperty.NoWrap, true, location));
            return true;
        }

        if (string.Equals(values[0].Text, "normal", StringComparison.OrdinalIgnoreCase))
        {
            assignments.Add(new MarkupAssignment(MarkupProperty.NoWrap, false, location));
            return true;
        }

        error = "Expected 'normal' or 'nowrap'.";
        return false;
    }

    private static bool AddPadding(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<int> values = IntValues(tokens, out error);
        if (error.Length > 0)
        {
            return false;
        }

        int left;
        int top;
        int right;
        int bottom;
        switch (values.Count)
        {
            case 1:
                left = values[0];
                top = values[0];
                right = values[0];
                bottom = values[0];
                break;
            case 2:
                left = values[0];
                right = values[0];
                top = values[1];
                bottom = values[1];
                break;
            case 4:
                left = values[0];
                top = values[1];
                right = values[2];
                bottom = values[3];
                break;
            default:
                error = "Padding requires 1, 2, or 4 integers.";
                return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.Padding, new[] { left, top, right, bottom }, location));
        return true;
    }

    private static bool AddCornerRadius(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<int> values = IntValues(tokens, out error);
        if (error.Length > 0)
        {
            return false;
        }

        int topLeft;
        int topRight;
        int bottomRight;
        int bottomLeft;
        switch (values.Count)
        {
            case 1:
                topLeft = values[0];
                topRight = values[0];
                bottomRight = values[0];
                bottomLeft = values[0];
                break;
            case 4:
                topLeft = values[0];
                topRight = values[1];
                bottomRight = values[2];
                bottomLeft = values[3];
                break;
            default:
                error = "Corner radius requires 1 or 4 integers.";
                return false;
        }

        //  Reef.CornerRadius stores TopLeft, TopRight, BottomLeft, BottomRight.
        assignments.Add(new MarkupAssignment(
            MarkupProperty.CornerRadius,
            new[] { topLeft, topRight, bottomLeft, bottomRight },
            location));
        return true;
    }

    private static bool AddBorder(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count == 0)
        {
            error = "Expected a border width and color.";
            return false;
        }

        if (values[0].Kind == RssTokenKind.Number)
        {
            if (!TryParseInt(values[0].Text, out int thickness))
            {
                error = "Border width must be an integer.";
                return false;
            }

            assignments.Add(new MarkupAssignment(MarkupProperty.BorderThickness, thickness, location));
            if (values.Count == 1)
            {
                return true;
            }

            var colorTokens = values.GetRange(1, values.Count - 1);
            if (!ColorParser.TryParse(colorTokens, out Vector4 color, out error))
            {
                return false;
            }

            assignments.Add(new MarkupAssignment(MarkupProperty.BorderColor, color, location));
            return true;
        }

        if (!ColorParser.TryParse(values, out Vector4 onlyColor, out error))
        {
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.BorderThickness, 1, location));
        assignments.Add(new MarkupAssignment(MarkupProperty.BorderColor, onlyColor, location));
        return true;
    }

    private static bool AddCornerCaps(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count == 0)
        {
            error = "Expected corner cap length, thickness, and color.";
            return false;
        }

        if (values[0].Kind == RssTokenKind.Number)
        {
            if (!TryParseInt(values[0].Text, out int length))
            {
                error = "Corner cap length must be an integer.";
                return false;
            }

            assignments.Add(new MarkupAssignment(MarkupProperty.CornerCapsLength, length, location));
            if (values.Count == 1)
            {
                return true;
            }

            if (values[1].Kind == RssTokenKind.Number)
            {
                if (!TryParseInt(values[1].Text, out int thickness))
                {
                    error = "Corner cap thickness must be an integer.";
                    return false;
                }

                assignments.Add(new MarkupAssignment(MarkupProperty.CornerCapsThickness, thickness, location));
                if (values.Count == 2)
                {
                    return true;
                }

                var colorTokens = values.GetRange(2, values.Count - 2);
                if (!ColorParser.TryParse(colorTokens, out Vector4 color, out error))
                {
                    return false;
                }

                assignments.Add(new MarkupAssignment(MarkupProperty.CornerCapsColor, color, location));
                return true;
            }

            var secondColorTokens = values.GetRange(1, values.Count - 1);
            if (!ColorParser.TryParse(secondColorTokens, out Vector4 secondColor, out error))
            {
                return false;
            }

            assignments.Add(new MarkupAssignment(MarkupProperty.CornerCapsColor, secondColor, location));
            return true;
        }

        if (!ColorParser.TryParse(values, out Vector4 onlyColor, out error))
        {
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.CornerCapsColor, onlyColor, location));
        return true;
    }

    private static bool AddConstraint(
        IReadOnlyList<RssToken> tokens,
        MarkupProperty property,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || !TryParseConstraint(values[0], out MarkupConstraint constraint, out error))
        {
            if (error.Length == 0)
            {
                error = "Expected a dimension such as 100, 50%, fill, fit, or shrink.";
            }

            return false;
        }

        assignments.Add(new MarkupAssignment(property, constraint, location));
        return true;
    }

    private static bool AddSize(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count == 0 || values.Count > 2)
        {
            error = "Size requires one or two dimensions.";
            return false;
        }

        if (!TryParseConstraint(values[0], out MarkupConstraint width, out error))
        {
            return false;
        }

        MarkupConstraint height = width;
        if (values.Count == 2 && !TryParseConstraint(values[1], out height, out error))
        {
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.Width, width, location));
        assignments.Add(new MarkupAssignment(MarkupProperty.Height, height, location));
        return true;
    }

    private static bool AddMinMaxSize(
        IReadOnlyList<RssToken> tokens,
        bool isMin,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<int> values = IntValues(tokens, out error);
        if (error.Length > 0)
        {
            return false;
        }

        if (values.Count != 1 && values.Count != 2)
        {
            error = "Size bounds require one or two integers.";
            return false;
        }

        int width = values[0];
        int height = values.Count == 2 ? values[1] : values[0];
        assignments.Add(new MarkupAssignment(isMin ? MarkupProperty.MinWidth : MarkupProperty.MaxWidth, width, location));
        assignments.Add(new MarkupAssignment(isMin ? MarkupProperty.MinHeight : MarkupProperty.MaxHeight, height, location));
        return true;
    }

    private static bool AddPosition(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 2)
        {
            error = "Position requires two dimensions.";
            return false;
        }

        if (!TryParseConstraint(values[0], out MarkupConstraint x, out error)
            || !TryParseConstraint(values[1], out MarkupConstraint y, out error))
        {
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.X, x, location));
        assignments.Add(new MarkupAssignment(MarkupProperty.Y, y, location));
        return true;
    }

    private static bool AddAnchors(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count == 0)
        {
            error = "Anchor requires at least one value.";
            return false;
        }

        MarkupAnchors anchors = MarkupAnchors.None;
        for (var i = 0; i < values.Count; i++)
        {
            RssToken token = values[i];
            if (token.Kind == RssTokenKind.Pipe)
            {
                continue;
            }

            if (token.Kind != RssTokenKind.Identifier)
            {
                error = "Anchor values must be names.";
                return false;
            }

            if (!TryParseAnchor(token.Text, out MarkupAnchors anchor))
            {
                error = "Unknown anchor '" + token.Text + "'.";
                return false;
            }

            if (anchor == MarkupAnchors.None)
            {
                if (anchors != MarkupAnchors.None)
                {
                    error = "Anchor 'None' cannot be combined with other anchors.";
                    return false;
                }

                anchors = MarkupAnchors.None;
                continue;
            }

            anchors |= anchor;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.Anchors, anchors, location));
        return true;
    }

    private static bool AddLayout(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || values[0].Kind != RssTokenKind.Identifier)
        {
            error = "Layout requires vertical, horizontal, or none.";
            return false;
        }

        MarkupLayoutDirection layout;
        if (string.Equals(values[0].Text, "vertical", StringComparison.OrdinalIgnoreCase))
        {
            layout = MarkupLayoutDirection.Vertical;
        }
        else if (string.Equals(values[0].Text, "horizontal", StringComparison.OrdinalIgnoreCase))
        {
            layout = MarkupLayoutDirection.Horizontal;
        }
        else if (string.Equals(values[0].Text, "none", StringComparison.OrdinalIgnoreCase))
        {
            layout = MarkupLayoutDirection.None;
        }
        else
        {
            error = "Layout requires vertical, horizontal, or none.";
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.Layout, layout, location));
        return true;
    }

    private static bool AddFontId(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || (values[0].Kind != RssTokenKind.Identifier && values[0].Kind != RssTokenKind.String))
        {
            error = "Font requires one font identifier.";
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.FontID, values[0].Text, location));
        return true;
    }

    private static bool AddHorizontalAlignment(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || values[0].Kind != RssTokenKind.Identifier)
        {
            error = "Text alignment requires left, center, or right.";
            return false;
        }

        MarkupHorizontalAlignment alignment;
        if (string.Equals(values[0].Text, "left", StringComparison.OrdinalIgnoreCase))
        {
            alignment = MarkupHorizontalAlignment.Left;
        }
        else if (string.Equals(values[0].Text, "center", StringComparison.OrdinalIgnoreCase))
        {
            alignment = MarkupHorizontalAlignment.Center;
        }
        else if (string.Equals(values[0].Text, "right", StringComparison.OrdinalIgnoreCase))
        {
            alignment = MarkupHorizontalAlignment.Right;
        }
        else
        {
            error = "Text alignment requires left, center, or right.";
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.HorizontalAlignment, alignment, location));
        return true;
    }

    private static bool AddVerticalAlignment(
        IReadOnlyList<RssToken> tokens,
        Location location,
        out List<MarkupAssignment> assignments,
        out string error)
    {
        assignments = [];
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        if (values.Count != 1 || values[0].Kind != RssTokenKind.Identifier)
        {
            error = "Vertical alignment requires top, center, or bottom.";
            return false;
        }

        MarkupVerticalAlignment alignment;
        if (string.Equals(values[0].Text, "top", StringComparison.OrdinalIgnoreCase))
        {
            alignment = MarkupVerticalAlignment.Top;
        }
        else if (string.Equals(values[0].Text, "center", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(values[0].Text, "middle", StringComparison.OrdinalIgnoreCase))
        {
            alignment = MarkupVerticalAlignment.Center;
        }
        else if (string.Equals(values[0].Text, "bottom", StringComparison.OrdinalIgnoreCase))
        {
            alignment = MarkupVerticalAlignment.Bottom;
        }
        else
        {
            error = "Vertical alignment requires top, center, or bottom.";
            return false;
        }

        assignments.Add(new MarkupAssignment(MarkupProperty.VerticalAlignment, alignment, location));
        return true;
    }

    private static bool TryParseConstraint(RssToken token, out MarkupConstraint constraint, out string error)
    {
        constraint = default;
        error = string.Empty;
        if (token.Kind == RssTokenKind.Identifier)
        {
            if (string.Equals(token.Text, "fill", StringComparison.OrdinalIgnoreCase))
            {
                constraint = new MarkupConstraint(MarkupConstraintKind.Fill, 0, 0f);
                return true;
            }

            if (string.Equals(token.Text, "fit", StringComparison.OrdinalIgnoreCase))
            {
                constraint = new MarkupConstraint(MarkupConstraintKind.Fit, 0, 0f);
                return true;
            }

            if (string.Equals(token.Text, "shrink", StringComparison.OrdinalIgnoreCase))
            {
                constraint = new MarkupConstraint(MarkupConstraintKind.Shrink, 0, 0f);
                return true;
            }

            error = "Unknown dimension keyword '" + token.Text + "'.";
            return false;
        }

        if (token.Kind != RssTokenKind.Number)
        {
            error = "Expected a dimension.";
            return false;
        }

        string text = token.Text;
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            if (!float.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
            {
                error = "Invalid percentage.";
                return false;
            }

            constraint = new MarkupConstraint(MarkupConstraintKind.Relative, 0, percent / 100f);
            return true;
        }

        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(0, text.Length - 2);
        }

        if (text.IndexOf('.') >= 0)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float relative))
            {
                error = "Invalid relative dimension.";
                return false;
            }

            constraint = new MarkupConstraint(MarkupConstraintKind.Relative, 0, relative);
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fixedValue))
        {
            error = "Invalid pixel dimension.";
            return false;
        }

        constraint = new MarkupConstraint(MarkupConstraintKind.Fixed, fixedValue, 0f);
        return true;
    }

    private static bool TryParseAnchor(string text, out MarkupAnchors anchor)
    {
        anchor = MarkupAnchors.None;
        if (string.Equals(text, "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(text, "top", StringComparison.OrdinalIgnoreCase))
        {
            anchor = MarkupAnchors.Top;
            return true;
        }

        if (string.Equals(text, "left", StringComparison.OrdinalIgnoreCase))
        {
            anchor = MarkupAnchors.Left;
            return true;
        }

        if (string.Equals(text, "bottom", StringComparison.OrdinalIgnoreCase))
        {
            anchor = MarkupAnchors.Bottom;
            return true;
        }

        if (string.Equals(text, "right", StringComparison.OrdinalIgnoreCase))
        {
            anchor = MarkupAnchors.Right;
            return true;
        }

        if (string.Equals(text, "center", StringComparison.OrdinalIgnoreCase))
        {
            anchor = MarkupAnchors.Center;
            return true;
        }

        if (string.Equals(text, "local", StringComparison.OrdinalIgnoreCase))
        {
            anchor = MarkupAnchors.Local;
            return true;
        }

        return false;
    }

    private static List<int> IntValues(IReadOnlyList<RssToken> tokens, out string error)
    {
        error = string.Empty;
        List<RssToken> values = NonTrivia(tokens);
        var result = new List<int>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i].Kind != RssTokenKind.Number || !TryParseInt(values[i].Text, out int value))
            {
                error = "Expected an integer.";
                return [];
            }

            result.Add(value);
        }

        return result;
    }

    private static bool TryParseInt(string text, out int value)
    {
        string number = text;
        if (number.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            number = number.Substring(0, number.Length - 2);
        }

        return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static List<RssToken> NonTrivia(IReadOnlyList<RssToken> tokens)
    {
        var result = new List<RssToken>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != RssTokenKind.Whitespace)
            {
                result.Add(tokens[i]);
            }
        }

        return result;
    }

    public static string Describe(IReadOnlyList<RssToken> tokens)
    {
        var parts = new List<string>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == RssTokenKind.Whitespace)
            {
                if (parts.Count > 0 && parts[parts.Count - 1] != " ")
                {
                    parts.Add(" ");
                }
            }
            else
            {
                parts.Add(tokens[i].Text);
            }
        }

        return string.Concat(parts).Trim();
    }
}
