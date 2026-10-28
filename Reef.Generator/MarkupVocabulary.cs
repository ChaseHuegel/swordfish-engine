using System;
using System.Collections.Generic;

namespace Reef.Generator;

internal static class MarkupVocabulary
{
    public static readonly HashSet<string> ROOT_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "Class",
        "Access",
        "Context",
        "RendererData",
        "Namespace",
    };

    public static readonly HashSet<string> COMMON_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "class",
        "visible",
        "color",
        "background",
        "padding",
        "corner-radius",
        "border",
        "border-width",
        "border-color",
        "corner-caps",
        "corner-caps-length",
        "corner-caps-thickness",
        "corner-caps-color",
        "overlay",
        "width",
        "height",
        "min-width",
        "min-height",
        "min-size",
        "max-width",
        "max-height",
        "max-size",
        "x",
        "y",
        "pos",
        "position",
        "anchor",
        "anchors",
    };

    public static readonly HashSet<string> ELEMENT_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "layout",
        "spacing",
        "size",
        "id",
        "onclick",
        "onrightclick",
        "onmiddleclick",
        "onheld",
        "button",
        "hotkey",
        "key",
        "disabled",
        "passthrough",
        "scroll",
        "scroll-x",
        "scroll-y",
        "onscroll",
        "clip",
        "clip-width",
        "clip-height",
    };

    public static readonly HashSet<string> TEXT_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "text",
        "size",
        "font",
        "text-align",
        "align",
        "vertical-align",
        "valign",
        "wrap",
        "font-size",
    };

    public static readonly HashSet<string> IMAGE_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "source",
        "size",
    };

    public static readonly HashSet<string> CONTENT_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "name",
    };

    public static readonly HashSet<string> CONDITION_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "condition",
    };

    public static readonly HashSet<string> FOREACH_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "in",
        "as",
        "index",
    };

    public static readonly HashSet<string> COMPONENT_CALL_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "context",
        "instance",
        "id",
        "visible",
        "class",
        "onclick",
        "onrightclick",
        "onmiddleclick",
        "onheld",
        "button",
        "hotkey",
        "key",
        "disabled",
        "passthrough",
        "scroll",
        "scroll-x",
        "scroll-y",
        "onscroll",
        "clip",
        "clip-width",
        "clip-height",
        "layout",
        "spacing",
    };

    public static readonly HashSet<string> STYLE_ATTRIBUTES = new(StringComparer.Ordinal)
    {
        "color",
        "background",
        "padding",
        "corner-radius",
        "border",
        "border-width",
        "border-color",
        "corner-caps",
        "corner-caps-length",
        "corner-caps-thickness",
        "corner-caps-color",
        "overlay",
        "width",
        "height",
        "size",
        "min-width",
        "min-height",
        "min-size",
        "max-width",
        "max-height",
        "max-size",
        "x",
        "y",
        "pos",
        "position",
        "anchor",
        "anchors",
        "layout",
        "spacing",
        "font",
        "font-size",
        "text-align",
        "align",
        "vertical-align",
        "valign",
        "wrap",
    };

    public static bool IsBuiltInTag(string tag)
    {
        return string.Equals(tag, "Element", StringComparison.Ordinal)
               || string.Equals(tag, "Text", StringComparison.Ordinal)
               || string.Equals(tag, "Image", StringComparison.Ordinal)
               || string.Equals(tag, "If", StringComparison.Ordinal)
               || string.Equals(tag, "ElseIf", StringComparison.Ordinal)
               || string.Equals(tag, "Else", StringComparison.Ordinal)
               || string.Equals(tag, "ForEach", StringComparison.Ordinal)
               || string.Equals(tag, "Empty", StringComparison.Ordinal)
               || string.Equals(tag, "Content", StringComparison.Ordinal);
    }

    public static bool IsStyleAttribute(string name)
    {
        return STYLE_ATTRIBUTES.Contains(name);
    }
}
