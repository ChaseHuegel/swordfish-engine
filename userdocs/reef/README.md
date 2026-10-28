# Reef Markup

Reef Markup is a compile-time UI language for Reef. It has two file types:

- `.rml` files contain XML markup.
- `.rss` files contain style rules.

A Roslyn source generator reads both file types during the build. The generator writes normal C# into the consumer project. The game never reads XML or CSS at run time.

## Start here

| Goal | Document |
|---|---|
| Set up the generator and write the first view | [getting-started](getting-started.md) |
| Copy complete examples | [examples](examples.md) |
| Look up a tag, attribute, style rule, or expression | [quick-reference](quick-reference.md) |
| See which test pins each specification rule | [spec-traceability](spec-traceability.md) |

The formal language contract is in
[reef-markup](../../docs/specs/reef-markup.md). The generator architecture is in
[reef-markup-blueprint](../../docs/specs/reef-markup-blueprint.md). The Reef
runtime contract is in [reef-ui](../../docs/specs/reef-ui.md).

## Feature summary

- XML markup with `Element`, `Text`, `Image`, `If`, `ElseIf`, `Else`,
  `ForEach`, `Empty`, and `Content` tags.
- Local `.rml` components with context values and content slots.
- Cross-assembly components with `ReefComponent` metadata.
- External C# widgets from imported XML namespaces.
- Style sheets with variables, inheritance, pseudo states, and dynamic classes.
- Expressions for context values, UI metrics, helper methods, and callbacks.
- Two-way binding for value widgets, change-result widgets, and `ref` state
  widgets.
- Layout constraints, anchors, padding, scrolling, clipping, and overlays.
- Compile-time diagnostics from `RF0001` to `RF0099`.
- XML schema support for editor completion.

## Minimal view

```xml
<Reef xmlns="Reef"
      Class="HelloView"
      Context="HelloState"
      Namespace="MyGame.UI">

    <Element layout="vertical" width="280" padding="12" background="#101820">
        <Text text="Hello {context.Name}" size="18" color="#ffffff" />
    </Element>

</Reef>
```

```csharp
namespace MyGame.UI;

public sealed class HelloState
{
    public string Name { get; set; } = "Player";
}

public partial class HelloView;
```

```csharp
var view = new HelloView();
view.Render(ui, context);
ui.Build(delta);
```

## Design rules

- The generator is compile-time only. Do not add an XML or CSS parser to the
  runtime.
- Use local `.rml` components before external C# widgets when you control the
  source.
- Use semantic style classes. Do not copy color literals into each view.
- Give every interactive element inside a loop a key-bound `id`.
- Use `bind` for simple two-way bindings. Use `value` and `onchange` when the
  edit needs validation or network work.

## Build and test

```bash
dotnet build
dotnet test Reef.Generator.Tests
dotnet test Reef.Markup.Tests
```

`Reef.Generator.Tests` checks parsing, diagnostics, binding, emission, and the
XML schema. `Reef.Markup.Tests` compiles real fixtures and checks layout,
style, input, components, and the visual gallery.
