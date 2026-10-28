# Reef Markup Implementation Plan

One subject: the approved implementation plan for the Reef Markup generator,
runtime support types, tests, and onboarding docs.

Status: implemented. The generator, runtime support types, test projects, and
`userdocs/reef/` onboarding docs are in the tree.

## Scope

Implement the full `.rml` and `.rss` compile-time pipeline described in
[reef-markup](reef-markup.md) and [reef-markup-blueprint](reef-markup-blueprint.md).

The change is engine-only. Do not modify `WaywardBeyond.*` projects.

## Locked decisions

| Topic | Decision |
|---|---|
| Bidirectional binding | Canonical spelling is `bind="context.X"`. `@bind` is not XML and is not supported. |
| Dynamic classes | `class:active="@condition"` requires `xmlns:class="Reef.Class"` on the root or an ancestor. |
| Bindable controls | Controls such as `Checkbox` and `Toggle` are prefixed external C# widgets. |
| Caller style API | Add `StyleOverride` with nullable fields. Generated Render methods accept `StyleOverride?`. |
| Caller constraint API | Add `ConstraintsOverride` with nullable fields. Generated Render methods accept `ConstraintsOverride?`. |
| Cross-assembly components | Implement with `ReefComponentAttribute`, slot delegates, and instance ID prefixes. |
| Component instances | Cross-assembly calls use `instance="@expression"` or an accessible parameterless constructor. |
| User documentation | New onboarding docs live under `userdocs/reef/`. |
| Clip behavior | `clip="false"` disables self-clipping. Ancestor clipping still applies. Use `overlay="true"` to escape. |
| Text size | On `<Text>`, `size` is font size. On `<Element>` and `<Image>`, `size` is width and height. |
| Background | `<Element>` maps `background` to the element fill. `<Text>` and `<Image>` map it to `BackgroundColor`. |

## Spec corrections

The implementation updates the spec and all examples:

- Replace `@bind` with `bind`.
- Add `xmlns:class="Reef.Class"` where dynamic class directives are used.
- Replace unprefixed bindable controls with prefixed widgets or local components.
- Replace `Style?` with `StyleOverride?`.
- Replace `Constraints?` with `ConstraintsOverride?`.
- Document `idPrefix` and component instance uniqueness.
- Document XSD association for IDEs.
- Document `clip` and `overlay` behavior.
- Document text `size` and layout `size`.
- Document per-node background mapping.

## Runtime additions

- `Reef/UI/StyleOverride.cs`
- `Reef/UI/ConstraintsOverride.cs`
- `Reef/ReefComponentAttribute.cs`

## Projects

| Project | Purpose |
|---|---|
| `Reef.Generator` | `netstandard2.0` Roslyn incremental generator |
| `Reef.Generator.Tests` | Pure parser, diagnostics, XSD, binding, emission, and incremental tests |
| `Reef.Markup.Tests` | Runtime layout, style, interaction, component, expression, docs-example, and visual tests |
| `Reef.Markup.TestComponents` | Compiled component and widget library for cross-assembly tests and examples |

## Generator structure

```text
Reef.Generator/
  Reef.Generator.csproj
  ReefGenerator.cs
  build/Reef.Generator.props
  Schemas/reef.xsd
  Common/
    CodeWriter.cs
    FnvHash.cs
    SourceLocation.cs
  Parsing/
    AdditionalFileItem.cs
    RmlParser.cs
    RssLexer.cs
    RssParser.cs
    ExpressionParser.cs
  Model/
    RmlDocument.cs
    RmlNode.cs
    ElementNode.cs
    TextNode.cs
    ImageNode.cs
    ContentNode.cs
    IfNode.cs
    ForEachNode.cs
    CustomWidgetNode.cs
    ComponentCallNode.cs
    RssRuleset.cs
    StyleRule.cs
    PseudoState.cs
  Analysis/
    StyleResolver.cs
    ColorParser.cs
    ConstraintParser.cs
    ExpressionBinder.cs
    SymbolBinder.cs
    ComponentRegistry.cs
    TypeResolver.cs
    DiagnosticDescriptors.cs
    MarkupVocabulary.cs
  Emission/
    CSharpEmitter.cs
    ComponentEmitter.cs
    WidgetEmitter.cs
```

## Generated API

```csharp
public void Render<TData>(
    UIBuilder<TData> ui,
    ContextType context,
    StyleOverride? callerStyle = null,
    ConstraintsOverride? callerConstraints = null,
    string? idPrefix = null,
    Action<UIBuilder<TData>>? content_default = null,
    Action<UIBuilder<TData>>? content_header = null)
```

`RendererData` replaces `TData` with a concrete type. `Context` removes the
context parameter. Slot parameters exist only for declared slots.

Generated component classes carry a `ReefComponent` attribute:

```csharp
[ReefComponent("ModalDialog", typeof(ModalState))]
public partial class ModalDialog
{
}
```

## Implementation phases

1. Engine API additions.
2. Generator scaffold, RML parser, XSD, diagnostics.
3. RSS lexer, parser, style resolver.
4. Expression parser and symbol binder.
5. Core emitter and control flow.
6. Components, slots, external widgets, cross-assembly.
7. Runtime, XSD, and visual tests.
8. User docs, solution, CI, and docs pass.

## Test strategy

- One or more tests for each spec section.
- One test for each diagnostic `RF0001` through `RF0033` and `RF0099`.
- Every valid fixture validated against `reef.xsd`.
- Every userdoc example compiled and executed.
- Visual gallery renders normal, hover, held, focused, and disabled frames.
- Golden PNG comparison with a 1/255 tolerance.
- Direct pixel assertions for critical colors, borders, caps, radii, and text.
- Incremental caching test for unrelated `.cs` edits.
- AOT contract test: generated source contains no runtime XML, CSS, or
  reflection calls.

## Diagnostics

The generator implements `RF0001` through `RF0015` from the spec and adds:

| ID | Purpose |
|---|---|
| `RF0016` | Invalid color value |
| `RF0017` | Invalid dimension or constraint |
| `RF0018` | Invalid shorthand value |
| `RF0019` | Unknown or invalid attribute |
| `RF0020` | Invalid expression |
| `RF0021` | Type mismatch |
| `RF0022` | Unknown stylesheet property |
| `RF0023` | Invalid enum value |
| `RF0024` | Duplicate element ID |
| `RF0025` | Circular component reference |
| `RF0026` | Missing required attribute |
| `RF0027` | Invalid class directive |
| `RF0028` | Invalid root, type, namespace, or identifier |
| `RF0029` | Duplicate component class |
| `RF0030` | Component slot or context mismatch |
| `RF0031` | Unsupported construct |
| `RF0032` | Unknown style class warning |
| `RF0033` | Scroll configuration warning |
| `RF0099` | Internal generator error |

## User documentation

Create under `userdocs/reef/`:

- `README.md`
- `getting-started.md`
- `examples.md`
- `quick-reference.md`
- `spec-traceability.md`

Use Simplified Technical English. Test every example.

## Quality gates

```bash
dotnet build
dotnet test Reef.Generator.Tests
dotnet test Reef.Markup.Tests
dotnet test Reef.Tests
dotnet test Reef.Text.Tests
dotnet test Swordfish.Tests
dotnet test WaywardBeyond.Voxels.Tests
```

Run the mandatory docs pass before finalizing.
