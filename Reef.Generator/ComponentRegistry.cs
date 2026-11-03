using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal sealed class ComponentRegistry
{
    private readonly Dictionary<string, RmlDocument> _components = new(StringComparer.Ordinal);

    public void Add(RmlDocument document, List<Diagnostic> diagnostics)
    {
        if (document.ClassName.Length == 0)
        {
            return;
        }

        if (_components.TryGetValue(document.ClassName, out RmlDocument? existing))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateComponent,
                document.ClassLocation,
                document.ClassName));
            return;
        }

        _components[document.ClassName] = document;
    }

    public bool TryGet(string name, out RmlDocument document)
    {
        return _components.TryGetValue(name, out document!);
    }

    public IEnumerable<RmlDocument> All => _components.Values;
}
