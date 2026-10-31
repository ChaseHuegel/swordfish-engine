# Reef Markup Specification

One subject: the Reef Markup Language (`.rml`) and Reef Style Sheet (`.rss`)
specification. This document defines the grammar, tags, attributes, style rules,
data bindings, input contracts, and code generation mappings for Reef UI.

Status: implemented by `Reef.Generator`. The onboarding path starts at
[userdocs/reef](../../userdocs/reef/README.md).

## Document structure

Every markup document is an XML file ending with `.rml`. The root node is
always `<Reef>`.

```xml
<Reef xmlns="Reef"
      xmlns:wb="WaywardBeyond.Client.UI"
      Class="MainMenuView"
      Context="MainMenuState"
      Namespace="WaywardBeyond.Client.UI.Views">
    ...
</Reef>
```

### Root attributes

- `Class` (required): The name of the generated C# partial class.
- `Access` (optional): Accessibility of the generated partial class (`public` or
  `internal`). Defaults to `public`. When omitted, if a companion partial class
  exists in the compilation, the generator automatically inherits the companion
  class's declared accessibility.
- `Context` (optional): The type of the data context passed to the render
  method. Access members on this instance with `@context.` or `@Context.`.
- `RendererData` (optional): Closes the generic `TRendererData` to a concrete
  type (such as `Swordfish.Graphics.Material`). When omitted, generated methods
  remain generic (`UIBuilder<TData>`).
- `Namespace` (optional): The namespace of the generated class. When omitted,
  the generator uses the folder path relative to the project root.
- `xmlns`: The default XML namespace for built-in Reef tags (`Reef`).
- `xmlns:<prefix>`: Prefix mappings for external C# namespaces and widgets.

A document may contain multiple sibling elements under `<Reef>`. Reef's builder
stores root elements in an internal list (`_closedRootElements`), allowing
multiple independent top-level root scopes (such as a full-screen base layer
plus an overlay watermark) within the same view. When caller styles or
constraints are passed to a multi-root component, they apply strictly to the
primary (first) root element. To size or position all sibling roots together,
wrap them in an outer `<Element>`.

### Empty views and stubs

A document with no child elements (e.g. `<Reef Class="PlaceholderView" Context="GameState" />`)
is valid. The generator emits a clean, no-op `Render` method body.

### Casing conventions

To maintain consistent XML schema validation and tooling autocomplete:

| Element | Casing convention | Examples |
|---|---|---|
| Tags | PascalCase | `<Reef>`, `<Element>`, `<Text>`, `<ForEach>` |
| Root attributes | PascalCase | `Class`, `Context`, `RendererData`, `Namespace` |
| Visual and layout attributes | lower-kebab-case | `min-width`, `corner-radius`, `border-color` |
| Event and state attributes | lowercase | `onclick`, `onheld`, `hotkey`, `visible`, `disabled` |
| Dynamic class directives | `class:<name>` | `class:active`, `class:selected` |

### Comments

Both markup and stylesheet files support their standard comment formats:

- **Markup (`.rml`)**: Standard XML comments `<!-- comment -->`. Comments can
  appear anywhere in the XML document. The parser ignores comments and does not
  emit code for them.
- **Stylesheets (`.rss`)**: Standard CSS block comments `/* comment */`. Comments
  can span multiple lines. The tokenizer discards comments during lexing.

### XML syntax rules and boolean attributes

Reef Markup strictly follows XML 1.0 grammar:

- **Explicit attribute values**: Unlike HTML, XML does not support valueless
  boolean attributes (such as `<Element overlay />`). All boolean attributes
  require explicit values (such as `overlay="true"`, `passthrough="true"`,
  `clip="false"`, `wrap="true"`). Writing valueless attributes triggers XML parser
  syntax error `RF0001`.
- **Self-closing tags**: Tags with no children must self-close (`<Text text="..." />`,
  `<Element height="8" />`).
- **Attribute names**: XML 1.0 does not allow `@` in an attribute name. Use
  `bind="context.IsFullscreen"`, not `@bind`. XML 1.0 also requires a declared
  prefix for a namespaced attribute. Use
  `xmlns:class="Reef.Class"` on the root before you use `class:active`.

## Tags

### Structural and layout tag: `<Element>`

Reef provides `<Element>` as its single, fundamental container and layout
primitive. Every `<Element>` compiles directly to an `Element<TRendererData>`
scope via `ui.Element(...)`:

```xml
<Element layout="vertical" spacing="8" class="panel">
    ...
</Element>
```

#### `<Element>` attributes

- `layout`: Sets `ui.LayoutDirection` (`vertical`, `horizontal`, `none`).
  When omitted, code generation does not emit an explicit `ui.LayoutDirection`
  assignment, allowing the underlying Reef element to determine its default.
- `spacing`: Integer pixel spacing between children along the layout axis
  (`ui.Spacing = ...`).
- `id`: Unique element string identifier for input tracking. If omitted on an
  interactive element, the generator derives a stable identifier.
- `onclick`: Callback executed on left mouse button click.
- `onrightclick`: Callback executed on right mouse button click.
- `onmiddleclick`: Callback executed on middle mouse button click.
- `onenter`: Callback executed when the pointer enters the element.
- `onexit`: Callback executed when the pointer leaves the element.
- `focus`: Boolean or expression. When true, the element takes keyboard focus.
- `button`: Active mouse button filter (`left`, `right`, `middle`). Default is
  `left`.
- `hotkey`: Keyboard key or dynamic key expression that triggers element interaction.
- `disabled`: Boolean or expression (`disabled="@context.IsLocked"`). When true,
  user input is suppressed and `:disabled` styling applies.
- `passthrough`: Boolean (`passthrough="true"`). When true, the element renders
  visually but is transparent to hit-testing.
- `overlay`: Boolean (`overlay="true"`). When true, allows the element to escape
  parent clipping bounds.
- `scroll`, `scroll-x`, `scroll-y`, `onscroll`: Viewport scrolling attributes.

#### Spacers and fillers

In immediate-mode layouts, empty spacing between elements is created using
`<Element>` with explicit or flexible constraints without introducing wrapper
tags or custom primitives:

- **Fixed vertical spacer**: `<Element height="8" />`
- **Fixed horizontal spacer**: `<Element width="16" />`
- **Expanding filler** (consumes remaining space to push siblings apart):
  `<Element width="fill" />` or `<Element size="fill" />`
- **External C# spacer widget**: `<wb:Spacer height="8" />` (when imported via
  `xmlns:wb`)

### Content primitive tags

Reef provides two content primitive tags mapping directly to Reef API calls:

| Tag | Target call | Purpose |
|---|---|---|
| `<Text>` | `ui.Text(...)` | Draws text with MSDF typeface metrics. |
| `<Image>` | `ui.Image(...)` | Draws a textured quad or material. |

Higher-level constructs (such as buttons, modals, or directional stacks) are
authored as reusable `.rml` components or C# extension widgets in consumer
libraries rather than built-in compiler magic.

#### `<Text>` attributes and content

Text content may be defined either via the `text` attribute (`<Text text="Hello" />`)
or as inner element body content (`<Text>Hello</Text>`). When both are present,
the `text` attribute takes precedence. Outer leading and trailing newlines and
whitespace around body text are trimmed automatically unless preserved with XML
`xml:space="preserve"`.

- `text`: String literal, string interpolation, or binding expression.
  Rich text formatting uses Reef's native `#` color run grammar (for example
  `#FF0000Red #RReset`). Inline formatting tags like `<Span>` are not supported.
- `size`: Font size in pixels. Defaults to `16`. On `<Element>` and
  `<Image>`, the `size` attribute is the width/height shorthand. On `<Text>`,
  `size` is always the font size. Use `width` and `height` for text dimensions.
- `font`: Font asset identifier (`FontOptions.ID`). When omitted, `FontOptions.ID`
  is null and inherits `UIBuilder.DefaultFontID` at runtime.
- `text-align` (or `align`): Horizontal text alignment within the element
  (`left`, `center`, `right`). Defaults to `left`.
- `vertical-align` (or `valign`): Vertical text alignment within the element
  (`top`, `center`, `bottom`). Defaults to `top`.
- `wrap`: Boolean enabling or disabling text wrapping (`true`, `false`).
  Setting `wrap="false"` enables `FontOptions.NoWrap`. Defaults to `true`.

##### Text wrapping rules

Reef automatically calculates line breaks when text width is constrained:

- When `<Text>` defines a constrained width (e.g. `width="200"`, `width="50%"`,
  or `width="fill"`) inside a parent container, text wraps automatically across
  multiple lines to fit the available space.
- When `<Text>` defines no width constraint, its width defaults to the text's
  unwrapped preferred width, preventing line breaks unless its parent container
  enforces a constrained bounding box.

#### `<Image>` attributes

- `source`: Texture or material reference. Must evaluate to `TRendererData`.
  Use property binding (`@context.AvatarTexture`) or method binding
  (`@GetIcon(item.Id)`). Resolving assets by file path inside the per-tick
  `Render` loop is not supported; pre-fetch assets in the view constructor.

### Control flow tags

Control flow tags do not emit layout elements. They control C# code generation
blocks directly.

#### `<If>`, `<ElseIf>`, `<Else>`

```xml
<If condition="@context.IsLoading">
    <Text text="Loading..." />
</If>
<ElseIf condition="@context.HasError">
    <Text text="Failed to load." class="text-error" />
</ElseIf>
<Else>
    <Text text="Ready." />
</Else>
```

##### Comparison operators and XML entities

In standard XML, raw `<` characters are invalid inside attribute values.
Condition expressions support both standard XML entities and keyword aliases:

| Operation | C# operator | XML entity | Keyword alias | Example |
|---|---|---|---|---|
| Less than | `<` | `&lt;` | `lt` | `@context.Gold lt context.Cost` |
| Less than or equal | `<=` | `&lt;=` | `lte` | `@context.Count lte 10` |
| Greater than | `>` | `&gt;` | `gt` | `@context.Hp gt 0` |
| Greater than or equal | `>=` | `&gt;=` | `gte` | `@context.Score gte 100` |
| Equal | `==` | `==` | `eq` | `@context.State eq 'ready'` |
| Not equal | `!=` | `!=` | `ne` | `@context.Tab ne 'closed'` |

Emitted C#:

```csharp
if (context.IsLoading)
{
    using (ui.Text("Loading...")) { }
}
else if (context.HasError)
{
    using (ui.Text("Failed to load."))
    {
        ui.Color = new Vector4(1f, 0.2f, 0.2f, 1f);
    }
}
else
{
    using (ui.Text("Ready.")) { }
}
```

#### `<ForEach>`

The `<ForEach>` tag repeats its children over a collection. It supports an
optional `<Empty>` tag rendered when the collection contains no items:

```xml
<Element layout="vertical" spacing="4">
    <ForEach in="@context.LobbyMembers" as="member" index="i">
        <Element layout="horizontal" spacing="8">
            <Text text="#{i}: {member.Name}" />
            <Element id="btn_{member.Id}" onclick="OnKick(member.Id)">
                <Text text="Kick" />
            </Element>
        </Element>
        <Empty>
            <Text text="No players in lobby." class="text-muted" />
        </Empty>
    </ForEach>
</Element>
```

##### Interactive element ID requirement in loops

Every interactive element (any `<Element>` declaring click, hover, or input
handlers) placed inside a `<ForEach>` must define an explicit, key-bound `id`
attribute (such as `id="btn_{member.Id}"`).

Omitting `id` on an interactive element inside `<ForEach>` produces compiler
diagnostic `RF0010`. Sharing an identifier across multiple loop iterations
causes immediate-mode interaction collisions in Reef's `UIController`.

A component body inlined inside a loop can derive its interactive `id` from a
parameter instead, when the call site passes a key-bound value:

```xml
<Stepper param:id="@row.Id" param:onStep="OnStep" />
```

The component owns the id suffix, such as `@(param.Id + '.previous')`, and the
call site owns uniqueness.

##### Lexical scoping in nested loops

Nested loops inherit full lexical scope over outer iteration variables and the
root context. An inner element can access inner loop items (`as="item"`), outer
loop items (`as="category"`), and root context fields (`@context.Property`)
without special path navigation syntax.

Emitted C#:

```csharp
using (ui.Element())
{
    ui.LayoutDirection = Reef.UI.LayoutDirection.Vertical;
    ui.Spacing = 4;

    bool hasAny = false;
    int i = 0;
    foreach (var member in context.LobbyMembers)
    {
        hasAny = true;
        using (ui.Element())
        {
            ui.LayoutDirection = Reef.UI.LayoutDirection.Horizontal;
            ui.Spacing = 8;
            using (ui.Text($"#{i}: {member.Name}")) { }
        }
        i++;
    }

    if (!hasAny)
    {
        using (ui.Text("No players in lobby."))
        {
            ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        }
    }
}
```

### Component projection tag: `<Content>`

The `<Content>` tag defines injection slots in reusable views:

```xml
<!-- ModalDialog.rml -->
<Reef xmlns="Reef" Class="ModalDialog">
    <Element layout="vertical" class="dialog-box">
        <Element layout="horizontal" class="dialog-header">
            <Content name="header">
                <Text text="Default Title" />
            </Content>
        </Element>
        <Element layout="vertical" class="dialog-body">
            <Content />
        </Element>
        <Element layout="horizontal" class="dialog-actions">
            <Content name="actions" />
        </Element>
    </Element>
</Reef>
```

At the call site:

```xml
<ModalDialog>
    <Content name="header">
        <Text text="Confirm Action" class="title-bold" />
    </Content>
    <Text text="Are you sure you want to proceed?" />
    <Content name="actions">
        <Element class="btn" onclick="OnCancel">
            <Text text="Cancel" />
        </Element>
        <Element class="btn btn-primary" onclick="OnProceed">
            <Text text="Proceed" />
        </Element>
    </Content>
</ModalDialog>
```

#### Component invocation, context passing, and styling

To invoke another `.rml` component and pass data or styling:

```xml
<ForEach in="@context.Players" as="player">
    <PlayerCard context="@player" class="player-highlight" width="240" />
</ForEach>
```

XSD 1.0 cannot validate this direct local component form while it enforces a
single `<Empty>` tag. Wrap the component in `<Element>` when your editor uses
the schema. The generator accepts both forms.

- When the child component declares `Context="PlayerInfo"`, pass the instance
  via `context="@expression"`.
- When the child component does not declare a `Context` (purely structural
  views), invoke it without the `context` attribute: `<FancyFrame>...</FancyFrame>`.
- The generator verifies at compile time that the expression passed to
  `context="..."` matches or converts to the child component's context type.
- **Native styling on `.rml` components**: Generated `Render` methods accept
  optional caller `StyleOverride?` and `ConstraintsOverride?` parameters. The
  override types use nullable fields, so an unset field leaves the component
  style in place. The root element of the child component merges caller styles
  and constraints with its internal styles. When inlined at compile time,
  caller styles merge directly into the child root element without allocating
  wrapper scopes. For components with multiple sibling roots, caller styles and
  constraints apply to the primary (first) root element.
- **Component identity**: Generated `Render` methods accept an optional
  `idPrefix` string. The generator uses this prefix for inner generated element
  IDs. The call site passes a stable instance prefix, so two instances of the
  same component do not collide.
- **Component parameters**: A component can declare a parameter model with
  `Parameters="ButtonParameters"`. Calls pass values through the `param:`
  namespace. Declare the namespace on the call document.

  ```xml
  <Reef xmlns="Reef"
        xmlns:param="Reef.Param"
        Class="ToolbarView">
      <Button param:variant="primary" param:compact="true">
          <Text text="Continue" />
      </Button>
  </Reef>
  ```

  The parameter type must expose writable public properties or fields. Every
  property must have a usable default. String, Boolean, numeric, enum, color,
  constraint, and expression values are supported. Local parameters are
  resolved by name and are case-insensitive at the call site. Cross-assembly
  components can expose a parameter model on their generated `Render` method,
  but markup parameter forwarding for cross-assembly components is not yet
  supported.
- **Disabled component calls**: A `disabled` attribute on a local component
  call guards the callbacks owned by that call. It also combines with any
  disabled state on the component root.
- **Cross-assembly components**: Generated component classes carry a
  `ReefComponent` attribute. A consumer project can use the tag directly. The
  component must have an accessible parameterless constructor, or the call site
  must pass `instance="@expression"`.

## Layout and constraint attributes

All dimensional attributes map to `Reef.Constraints.IConstraint` implementations.

### Box model semantics (Border-box)

Reef layout adheres to standard border-box semantics for constrained axes:

- When `width` or `height` is `Fixed` (e.g. `width="100"`) or `Relative` (e.g.
  `width="50%"`), the constraint sets the **total outer dimension** of the
  element. Padding reduces the inner area available for children. An element
  with `width="100"` and `padding="10"` occupies exactly 100 pixels, leaving 80
  pixels of inner space for content.
- When `width` or `height` is `Fit` or unconstrained, padding expands the
  element's outer bounding box around its children.

### Width and height

- `100` or `100px`: Fixed pixels (`new Fixed(100)`).
- `50%` or `0.5`: Relative fraction of parent (`new Relative(0.5f)`).
- `fill`: Fills remaining space along the axis (`new Fill()`).
- `fit`: Sizes to fit children bounds (`new Fit()`).
- `shrink`: Shrinks when parent compresses (`new Shrink()`).

A `fill` element never grows past its parent's allotted space. Its minimum
width comes from its children only when its parent is `fit` or `shrink`, so
text in a `fit` parent sizes to its text. Wider content in any other parent
overflows the element. Set `clip` on the parent to hide it, or give the content
a fixed width. Overflow is not clipped by default.

An expression can return an integer or `IConstraint`. An integer maps to
`Fixed`. An `IConstraint` value is emitted directly, so a model can expose
dynamic fill or relative widths.

Compound attribute `size` sets both axes:

- `size="fill"` $\rightarrow$ `width="fill"` and `height="fill"`.
- `size="200 100"` $\rightarrow$ `width="200"` and `height="100"`.
- `size="100% fill"` $\rightarrow$ `width="100%"` and `height="fill"`.

### Min and max bounds

- `min-width="120"`: Maps to `Constraints.MinWidth = 120`.
- `min-height="40"`: Maps to `Constraints.MinHeight = 40`.
- `min-size="120 40"`: Sets both minimum bounds.
- `max-width="500"`: Maps to `Constraints.MaxWidth = 500`.
- `max-height="300"`: Maps to `Constraints.MaxHeight = 300`.
- `max-size="500 300"`: Sets both maximum bounds.

### Position offsets

- `x="10"`: Maps to `Constraints.X = new Fixed(10)`.
- `y="20"`: Maps to `Constraints.Y = new Fixed(20)`.
- `pos="10 20"` or `position="10 20"`: Sets both offsets.

### Anchors

The `anchor` (or `anchors`) attribute maps to `Reef.UI.Anchors`:

- Values: `None`, `Top`, `Bottom`, `Left`, `Right`, `Center`, `Local`.
- Combinations are space-separated or pipe-separated: `anchor="Top Right"`, `anchor="Center Local"`.

### Padding

Padding follows the .NET and XAML LTRB standard: **Left, Top, Right, Bottom**.

- 1 value: `padding="10"` $\rightarrow$ all sides 10 (`new Padding(10, 10, 10, 10)`).
- 2 values: `padding="10 20"` $\rightarrow$ Horizontal Vertical (`new Padding(10, 20, 10, 20)`).
- 4 values: `padding="10 20 30 40"` $\rightarrow$ Left Top Right Bottom (`new Padding(10, 20, 30, 40)`).

### Spacing

- `spacing="8"`: Pixel gap between children along the layout axis.

## Styling attributes and stylesheets

### Visual attributes

- `color`: Element text or tint color (`Style.Color`). Accepts 3/4/6/8-character hex
  values (`#fff`, `#ffffff80`), named colors, variables, or functions:
  `rgb(255, 255, 255)` and `rgba(255, 255, 255, 0.5)`. The generator parses
  colors into constant `Vector4` literals at compile time.
- `text-color`: Inherited text color for descendant `Text` elements that do
  not set an explicit color. Use it for button, tab, and icon states.
- `background`: Element background color (`Style.BackgroundColor`). On
  `<Element>`, the generator also applies it to the element fill (`Style.Color`)
  because the renderer uses the fill for plain elements. On `<Text>` and
  `<Image>`, it sets `Style.BackgroundColor`. Accepts the same formats as
  `color`.
- `corner-radius`: Corner radius. Single integer (`4`), or four space-separated
  integers for `TopLeft TopRight BottomRight BottomLeft` (`4 4 0 0`).
- `border`: Shorthand for width and color: `border="1 #444444"`.
- `border-width`: Integer pixel border width.
- `border-color`: Border color.
- `border-top`, `border-right`, `border-bottom`, `border-left`: Per-side border
  shorthand. A zero thickness uses the shared border. A side color with zero
  alpha uses the shared color.
- `overlay`: Boolean (`true`, `false`). When `true`, allows an element to escape
  its parent's clipping bounds, painting over the parent or grandparent without
  being clipped (used for tooltips, dropdowns, context menus).
- `corner-caps`: Sci-fi HUD bracket style (`CornerCapStyle`).
  Shorthand syntax: `corner-caps="length thickness color"`, for example
  `corner-caps="8 2 #00ffff"`.
- `corner-caps-length`, `corner-caps-thickness`, `corner-caps-color`: Discrete
  corner cap properties.

### Stylesheet format (`.rss`)

Styles live in external `.rss` files discovered automatically as
`AdditionalFiles`. Style sheets use CSS-compatible syntax: `--extends` (or
`--extend`) for class inheritance, `!important` for variable overrides, and `px`
units for integer properties. The generator also accepts the legacy `.sub : .base`
and `override --name` forms.

```css
:root {
    --bg-surface: #1e1e1e;
    --accent: #3b82f6;
    --accent-hover: #60a5fa;
    --text-main: #ffffff;
    --radius-sm: 4px;
}

.panel {
    background: var(--bg-surface);
    padding: 16px;
    corner-radius: var(--radius-sm);
}

.btn {
    padding: 8px 16px;
    background: #2a2a2a;
    color: var(--text-main);
    corner-radius: var(--radius-sm);
    border: 1px #444444;
}

.btn:hover {
    background: #3a3a3a;
}

.btn:active {
    background: #1a1a1a;
}

/* Style inheritance: .btn-primary subclasses .btn */
.btn-primary {
    --extends: btn;
    background: var(--accent);
    border: 1px #2563eb;
}

.btn-primary:hover {
    background: var(--accent-hover);
}

/* Multiple inheritance: .btn-danger-large subclasses .btn and .large */
.btn-danger-large {
    --extends: btn large;
    background: #ef4444;
}
```

### Class inheritance and multiple base classes

Classes inherit properties using the `--extends` (or `--extend`) custom
property. Base classes are referenced as CSS identifiers without a leading dot
(`btn` rather than `.btn`), avoiding CSS term inspection errors in IDEs and
matching the `class="btn"` attribute convention in markup.

Multiple base classes are supported via space-separated or comma-separated
identifiers:

```css
.card-highlight {
    --extends: card shadow animated;
    border-color: var(--accent);
}
```

Inheritance evaluates from left to right: earlier base classes are applied first,
subsequent base classes override earlier ones, and properties declared directly
in the child class take final precedence. Base pseudo-states (such as `:hover` or
`:active`) are also inherited unless explicitly overridden in the child rule.
Circular inheritance raises compiler error `RF0003`. If a base class referenced
in `--extends` does not exist in any loaded stylesheet, the compiler halts with
error `RF0034` (`Undefined base class in --extends inheritance`).

### Supported stylesheet properties

Rulesets in `.rss` support four categories of properties:

1. **Visual style**: `color`, `text-color`, `background`, `background-color`,
   `corner-radius`, `border-radius`, `border`, `border-width`, `border-color`,
   `border-top`, `border-right`, `border-bottom`, `border-left`, `overlay`,
   `corner-caps`, `corner-caps-length`, `corner-caps-thickness`,
   `corner-caps-color`.
2. **Typography**: `font-size` (`FontOptions.Size`), `font-family` (preferred
   for CSS tools) or `font` (`FontOptions.ID`), `text-align` (`left`, `center`, `right`), `vertical-align`
   (`top`, `middle`, `bottom`). The alias `center` is also accepted. `white-space` (`nowrap`, `normal`).
3. **Dimensions and constraints**: `width`, `height`, `size`, `min-width`,
   `min-height`, `min-size`, `max-width`, `max-height`, `max-size`, `pos`,
   `x`, `y`, `anchor`, `anchors`.
4. **Layout**: `padding`, `spacing`, `gap`, `layout` (`vertical`, `horizontal`, `none`).

The aliases `background-color`, `border-radius`, and `gap` map to
`BackgroundColor`, `CornerRadius`, and `Spacing`.

### Multi-file variables and overrides

All `:root` blocks across all `.rss` files in the project aggregate into a
single global variable table at compile time.

To prevent silent collisions, defining the same variable name in multiple files
produces compiler error `RF0012`. To intentionally override a variable
inherited from a base theme or package, mark the declaration with
`!important`:

```css
/* CoreTheme.rss (imported package) */
:root {
    --color-primary: #3b82f6;
}

/* GameTheme.rss (local project) */
:root {
    /* Explicitly overrides the base theme variable */
    --color-primary: #ffaa00 !important;

    /* Local variable */
    --game-health-bar: #22c55e;
}
```

If two files both mark `--name` with `!important`, the generator halts with
error `RF0013`. The legacy `override --name` form is still accepted. Use the
`!important` form so CSS editors parse the file.

### Multi-file class cascading and merging

When multiple `.rss` files declare the same class selector (such as a local game
theme customizing a shared base theme's `.btn` class), the declarations cascade
and merge in file discovery order. Properties declared in subsequent rules
override properties from earlier rules, and newly declared properties augment
the class. This allows project stylesheets to customize design system classes
without repeating the entire ruleset.

### Pseudo-state resolution

The generator compiles `:hover`, `:active`, `:focus`, and `:disabled` states
into immediate-mode branching without runtime dictionary lookups:

```csharp
bool disabled = context.IsDisabled;
bool hovering = !disabled && ui.Hovering();
bool held = !disabled && ui.Held();
ui.BackgroundColor = disabled ? DisabledColor
                   : held ? ActiveColor
                   : hovering ? HoverColor
                   : NormalColor;
```

When an element defines `disabled="@condition"` (or `disabled="true"`), click
and input events are suppressed automatically in the generated C# code.

### Dynamic class directives (`class:<name>`)

To conditionally toggle style classes without duplicating elements in `<If>`
blocks, use the `class:<name>="@condition"` directive. XML 1.0 requires a
declared prefix for the attribute:

```xml
<Reef xmlns="Reef" xmlns:class="Reef.Class" Class="TabView">
<Element class="tab"
        class:active="@context.IsTabActive('audio')"
        class:disabled="@context.IsTabLocked"
        onclick="SelectTab('audio')">
    <Text text="Audio" />
</Element>
</Reef>
```

Emitted C#:

```csharp
bool isActive = context.IsTabActive("audio");
ui.BackgroundColor = isActive ? TabActiveBg : TabNormalBg;
ui.Border = isActive ? TabActiveBorder : TabNormalBorder;
```

### Precedence and class evaluation order

When an element defines multiple classes, classes evaluate strictly from left
to right in attribute order. Later classes override properties defined by earlier
classes:

```xml
<!-- .btn-warning overrides background from .btn; .text-large overrides font-size -->
<Element class="btn btn-warning text-large" />
```

Final property precedence resolves in this order:

1. Stylesheet base rule (`.btn`)
2. Stylesheet inherited rule (`.btn-primary { --extends: btn; }`)
3. Sibling classes in attribute order (`class="a b"`: `b` overrides `a`)
4. Dynamic class directives (`class:name="@condition"`, evaluated if true)
5. Pseudo-state rule (`.btn:hover`, `:active`, `:disabled`)
6. Inline markup attribute (`<Element background="#ff0000" />`)

## Data binding and expressions

### Direct property binding and UI environment access

Prefix expressions with `@context.` to read data fields, or `@ui.` to read
runtime layout and input metrics:

- `ui.Delta`: Frame delta time.
- `ui.Time`: Accumulated engine time.
- `ui.Width`, `ui.Height`: Current viewport pixel dimensions.
- `ui.IsMouseCaptured`: True when mouse input is captured by the UI.
- `ui.IsKeyboardCaptured`: True when keyboard input is captured by the UI.
- `ui.GetScrollDelta()`: Wheel scroll float delta.
- `ui.GetScrollDelta2D()`: Wheel scroll 2D vector delta.
- `ui.GetElementBounds('id')`: Resolved bounds of a previously rendered element.

```xml
<Text text="@context.Username" />
<Image source="@context.PulseTexture" color="@GetPulseColor(ui.Time)" />
<Text text="Bounds: {ui.GetElementBounds('player_card').Rect.Size.X}px" />
```

### String interpolation and literal braces

Enclose expressions in curly braces `{}` inside string attributes:

```xml
<Text text="Level {context.Level} - Gold: {context.Gold}" />
```

To render literal `{` or `}` characters, escape them with double braces `{{` and `}}`:

```xml
<Text text="Braced: {{escaped}} and {context.Value}" />
```

Double braces map directly to standard C# interpolated string escapes (`$"{{literal}}"`).

### Method binding, localization, and multi-argument evaluation

Bind method calls directly. When an expression does not prefix `@context.`, the
generator targets the View code-behind. Method bindings support multiple
arguments, allowing combinations of context data, literals, and runtime UI
metrics (such as `ui.Delta` and `ui.Time`):

```xml
<!-- Calls View method: partial string Localize(string key); -->
<Text text="@Localize('ui.menu.title')" />

<!-- Calls View method with single argument: -->
<Text text="@FormatCurrency(context.Gold)" />

<!-- Calls View method with multiple arguments combining data and engine delta: -->
<Text text="@FormatProgress(context.Cooldown, ui.Delta)" />

<!-- Calls View method calculating dynamic animation from engine time: -->
<Image source="@context.Icon" color="@GetPulseColor(context.Rarity, ui.Time)" />

<!-- Calls Context method with arguments: -->
<Text text="@context.CalculateScore(context.Streak, ui.Time)" />
```

The generator derives argument types from Roslyn symbols and emits the
corresponding `partial` method signature. Single-quoted strings in markup
arguments transpile to C# double-quoted strings (e.g. `'ui.menu.title'`
becomes `"ui.menu.title"`).

Under C# language rules, partial methods returning a value require an explicit
accessibility modifier. The generator emits `protected partial` for non-void
methods, enforcing at compile time that the companion class implements them:

```csharp
protected partial string Localize(string key);
protected partial string FormatProgress(float cooldown, double delta);
protected partial System.Numerics.Vector4 GetPulseColor(int rarity, double time);
```

Void event callbacks without return values are emitted as `partial void` without
accessibility modifiers:

```csharp
partial void OnStartGame();
partial void OnSelectTab(string tabName);
```

### Two-way binding for editing and forms

For editable controls (such as checkboxes, text boxes, and sliders), the `bind`
attribute connects the widget to a mutable property or field on the context.

```xml
<!-- Value-based two-way binding -->
<wb:Checkbox id="cb_fullscreen" text="Fullscreen" bind="context.IsFullscreen" />

<!-- State struct two-way binding passed by ref -->
<wb:TextBox id="chat_input" bind="context.ChatBoxState" />
```

#### Emitted C#

For value-returning widgets (`Checkbox`), the generator emits change detection
and writeback:

```csharp
bool newIsFullscreen = Widgets.Checkbox(ui, id: "cb_fullscreen", text: "Fullscreen", value: context.IsFullscreen);
if (newIsFullscreen != context.IsFullscreen)
{
    context.IsFullscreen = newIsFullscreen;
    OnIsFullscreenChanged(newIsFullscreen);
}
```

For widgets taking a `ref` state struct (`TextBoxState`), the generator passes
the field by reference:

```csharp
ui.TextBox("chat_input", ref context.ChatBoxState, ...);
```

#### Decoupled alternative

If custom validation, clamping, or networking is required before applying an
edit, decouple the binding into explicit `value` and `onchange` attributes:

```xml
<wb:Checkbox id="cb_mute" text="Mute Audio"
             value="@context.IsMuted"
             onchange="OnMuteToggled" />
```

## Input and callbacks

### Hybrid callback contract and multi-argument events

Callbacks target either the View code-behind or the Context. Event attributes
(`onclick`, `onrightclick`, `onmiddleclick`, `onenter`, `onexit`, `onscroll`) support multiple
comma-separated arguments combining context properties, iteration variables,
string literals, and runtime UI metrics (such as `ui.Delta` and `ui.GetScrollDelta()`):

1. **View code-behind (default)**:
   ```xml
   <Element onclick="OnStartGame" />
   <Element onclick="OnSelectTab('audio')" />
   <Element onclick="OnKickPlayer(member.Id)" />
   <Element onclick="OnApplySetting('volume', context.Volume, ui.Delta)" />
   <Element scroll="vertical" onscroll="OnScroll(tab.Id, ui.GetScrollDelta())" />
   ```
   The generator emits matching `partial void` declarations in the generated
   class, deriving parameter types from Roslyn semantic symbols:
   ```csharp
   partial void OnStartGame();
   partial void OnSelectTab(string tabName);
   partial void OnKickPlayer(string playerId);
   partial void OnApplySetting(string settingKey, float value, double delta);
   partial void OnScroll(string tabId, float scrollDelta);
   ```

2. **Context method invocation**:
   ```xml
   <Element onclick="@context.StartGame" />
   <Element onclick="@context.SelectTab('audio')" />
   <Element onclick="@context.ApplySetting('volume', context.Volume)" />
   ```
   The generator invokes the method directly on the context instance.

3. **Delegate member invocation**:
   ```xml
   <Element onclick="@param.OnDecrease" />
   <Element onclick="@param.OnStep(row.Id, -1)" />
   ```
   When the member holds a delegate, the generator emits a null-safe
   `?.Invoke(...)` and forwards any arguments. This lets a component forward its
   own control events to the caller.

A component parameter of delegate type accepts a view method name, so a
component can own its controls and still run the caller's handler:

```xml
<Stepper param:id="@row.Id" param:onStep="OnStep" />
```

### Dynamic hotkeys and keybindings

The `hotkey` and `key` attributes support three value sources on interactive `<Element>` tags:

1. **Enum literal**:
   ```xml
   <Element hotkey="Esc" onclick="OnQuit">
       <Text text="Quit" />
   </Element>
   ```
2. **Context property binding**:
   ```xml
   <Element hotkey="@context.InventoryKey" onclick="OnToggleInventory">
       <Text text="Inventory" />
   </Element>
   ```
3. **Dynamic input mapping method**:
   ```xml
   <Element hotkey="@GetKeyBinding('gameplay.interact')" onclick="OnInteract">
       <Text text="Interact" />
   </Element>
   ```
   The generator emits `partial Reef.UIController.Key GetKeyBinding(string action);`.
   The developer queries their game input service in the implementation.

## Viewport and clipping

### Scrolling

- `scroll`: Scroll mode (`none`, `vertical`, `horizontal`, `both`).
- `scroll-x`: Horizontal scroll offset integer or binding (`scroll-x="@context.ScrollX"`).
- `scroll-y`: Vertical scroll offset integer or binding (`scroll-y="@context.ScrollY"`).
- `onscroll`: Scroll callback triggered when the user scrolls over this element
  (e.g. `onscroll="OnScrolled"` or `onscroll="OnScrolled(ui.GetScrollDelta())"`).
- `onenter` / `onexit`: Pointer entry and exit callbacks. The element must have
  an `id` so the controller can track the edge.
- `focus="true"`: Calls `Focus()` on the element each frame while true.

### Clipping and overlay

- `clip="true"`: Clips children strictly to the element bounding box.
- `clip="false"`: Disables self-clipping. Ancestor clipping still applies. Use `overlay="true"` to escape ancestor clipping.
- `clip-width="200"`, `clip-height="100"`: Custom clip rectangle constraints.
- `clip="200 100"`: Shorthand for clip width and height.
- `overlay="true"`: Escapes parent clipping bounds, allowing child elements
  (such as tooltips, context menus, and dropdowns) to render over outer parent
  containers without being clipped.

### Rendering order, Z-order, and input capture

Reef guarantees the Painter's Algorithm and strict input precedence:

1. **Painter's Algorithm (Z-Order)**:
   - A parent background always paints behind its child elements.
   - Sibling elements paint in document order: later siblings paint over
     earlier siblings.
   - When overlaying children within an unaligned container (`layout="none"`),
     the first declared child is the bottom layer, and subsequent children
     render on top of previous layers.

2. **Input Capture**:
   - Reef processes input in reverse render order (top-most element first).
   - The top-most element under the cursor captures mouse interactions
     (`Hovering`, `Clicked`, `Held`).
   - If an element defines `passthrough="true"`, it renders visually but is
     ignored during hit-testing, allowing elements underneath to receive clicks.

## Namespaces and custom widgets

Declare external namespaces via standard XML `xmlns:<prefix>` attributes:

```xml
<Reef xmlns="Reef"
      xmlns:wb="WaywardBeyond.Client.UI"
      Class="HudView"
      Context="HudState">

    <Element layout="vertical">
        <wb:TextButton id="chat_toggle" text="Chat" onclick="ToggleChat" />
    </Element>
</Reef>
```

The generator emits the matching `using WaywardBeyond.Client.UI;` directive and
calls the static or extension method `Widgets.TextButton(...)`.

### Tag resolution priority

When resolving element tags during compilation:

1. **Built-in tags**: `<Element>`, `<Text>`, `<Image>`, `<If>`, `<ElseIf>`, `<Else>`,
   `<ForEach>`, `<Empty>`, `<Content>`.
2. **Local `.rml` components**: Any `.rml` file whose `<Reef Class="...">` matches
   the tag name (e.g. `<PlayerCard ... />`). Common UI constructs such as `Button`,
   `Vertical`, `Horizontal`, and `Stack` can be authored as local components or
   provided by a theme/component package.
3. **Referenced `.rml` components**: A generated component in a referenced
   assembly that carries a `ReefComponent` attribute with the matching name.
4. **External C# widgets**: Methods matching the tag name in imported XML namespaces
   (e.g. `<wb:TextButton ... />`).
5. If no target matches, the compiler reports error `RF0015` (`Unknown element or component <TagName>`).

### Styling external C# widgets

For hand-written C# widgets (such as `Widgets.TextButton` or `Widgets.Checkbox`):

1. **Direct Parameter Passing**: If the C# extension method signature declares
   parameters of type `Style`, `Constraints`, or `FontOptions`, the generator
   passes the resolved caller styling directly into the matching arguments.
2. **Scope Wrapping Fallback**: If the C# extension method does not declare style
   parameters, the generator wraps the widget call in an outer styling
   `using (ui.Element())` scope:

```csharp
using (ui.Element())
{
    ui.Constraints = new Constraints { Width = new Fixed(200) };
    ui.Padding = new Padding(8, 8, 8, 8);
    WaywardBeyond.Client.UI.Widgets.TextButton(ui, "btn_id", "Play", ...);
}
```

This guarantees uniform outer dimensions, margins, and positioning across all
widgets. Caveat: internal properties hardcoded inside legacy C# methods (such as
fixed inner label colors) cannot be modified by an outer wrapper element.
Authoring reusable components as `.rml` files avoids this limitation entirely.

### Custom widget child elements

When an external C# widget (`<wb:Widget>`) contains child elements in markup:

- **Disposable scopes**: If the extension method returns `System.IDisposable`
  (such as `ui.Element(...)`), child elements emit inside the `using (...) { ... }`
  block.
- **Void and value widgets**: If the extension method returns `void` or a value
  type (such as `ui.Checkbox(...)`), defining child elements in markup is invalid
  and emits compiler error `RF0014` (`Void widget cannot contain child elements`).

## Complete end-to-end example

### Stylesheet (`HudTheme.rss`)

```css
:root {
    --bg-dark: #141414;
    --accent: #00bcd4;
}

.panel {
    background: var(--bg-dark);
    padding: 12px 16px;
    corner-radius: 4px;
    border: 1px #333333;
}

.action-btn {
    padding: 6px 12px;
    background: #222222;
    color: #ffffff;
    corner-radius: 4px;
}

.action-btn:hover {
    background: #333333;
}

.action-btn:active {
    background: #111111;
}
```

### Markup (`HudView.rml`)

```xml
<Reef xmlns="Reef"
      Class="HudView"
      Context="HudState"
      Namespace="WaywardBeyond.Client.UI">

    <Element layout="none" width="100%" height="100%">
        <!-- Top Bar -->
        <Element layout="horizontal" class="panel" width="100%" height="48" anchor="Top" spacing="12">
            <Text text="@Localize('hud.title')" size="18" />
            <Text text="Player: {context.PlayerName}" />
            <Element width="fill" />
            <Element class="action-btn"
                    hotkey="@GetKeyBinding('menu.pause')"
                    onclick="OnPauseClicked">
                <Text text="Pause" />
            </Element>
        </Element>

        <!-- Notification List -->
        <Element layout="vertical" width="300" anchor="Bottom Right" padding="0 0 16 16" spacing="4">
            <ForEach in="@context.Notifications" as="item">
                <Element layout="horizontal" class="panel" width="100%">
                    <Text text="@item.Message" />
                </Element>
            </ForEach>
        </Element>
    </Element>
</Reef>
```

### Hand-written code-behind (`HudView.cs`)

```csharp
namespace WaywardBeyond.Client.UI;

public partial class HudView
{
    private readonly ILocalizationService _localization;
    private readonly IInputMappingService _inputMapping;

    public HudView(ILocalizationService localization, IInputMappingService inputMapping)
    {
        _localization = localization;
        _inputMapping = inputMapping;
    }

    protected partial string Localize(string key) => _localization.Get(key);

    protected partial Reef.UIController.Key GetKeyBinding(string action) => _inputMapping.GetKeyForAction(action);

    partial void OnPauseClicked()
    {
        // Trigger pause menu
    }
}
```

### Generated source (`HudView.g.cs`)

```csharp
// <auto-generated by Reef.Generator />
#nullable enable
using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;

namespace WaywardBeyond.Client.UI;

public partial class HudView
{
    protected partial string Localize(string key);
    protected partial Reef.UIController.Key GetKeyBinding(string action);
    partial void OnPauseClicked();

    public void Render<TData>(UIBuilder<TData> ui, HudState context, StyleOverride? callerStyle = null, ConstraintsOverride? callerConstraints = null, string? idPrefix = null)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
                Height = new Relative(1f)
            };

            // Top Bar
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.Horizontal;
                ui.Spacing = 12;
                ui.Constraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Fixed(48),
                    Anchors = Anchors.Top
                };
                ui.Padding = new Padding(12, 16, 12, 16);
                ui.CornerRadius = new CornerRadius(4);
                ui.BackgroundColor = new Vector4(0.078f, 0.078f, 0.078f, 1f);
                ui.Border = new BorderStyle { Thickness = 1, Color = new Vector4(0.2f, 0.2f, 0.2f, 1f) };

                string titleText = Localize("hud.title");
                using (ui.Text(titleText, size: 18)) { }

                using (ui.Text($"Player: {context.PlayerName}")) { }

                using (ui.Element())
                {
                    ui.Constraints = new Constraints
                    {
                        Width = new Fill()
                    };
                }

                using (ui.Element("hud_btn_pause"))
                {
                    bool hovering = ui.Hovering();
                    bool held = ui.Held();
                    bool clicked = ui.Clicked();

                    UIController.Key boundKey = GetKeyBinding("menu.pause");
                    bool hotkeyPressed = ui.IsPressed(boundKey);

                    ui.Padding = new Padding(6, 12, 6, 12);
                    ui.CornerRadius = new CornerRadius(4);
                    ui.BackgroundColor = held ? new Vector4(0.067f, 0.067f, 0.067f, 1f)
                                       : hovering ? new Vector4(0.2f, 0.2f, 0.2f, 1f)
                                                  : new Vector4(0.133f, 0.133f, 0.133f, 1f);
                    ui.Color = new Vector4(1f, 1f, 1f, 1f);

                    using (ui.Text("Pause")) { }

                    if (clicked || hotkeyPressed)
                    {
                        OnPauseClicked();
                    }
                }
            }

            // Notification List
            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.Vertical;
                ui.Spacing = 4;
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(300),
                    Anchors = Anchors.Bottom | Anchors.Right
                };
                ui.Padding = new Padding(0, 0, 16, 16);

                foreach (var item in context.Notifications)
                {
                    using (ui.Element())
                    {
                        ui.LayoutDirection = LayoutDirection.Horizontal;
                        ui.Constraints = new Constraints
                        {
                            Width = new Relative(1f)
                        };
                        ui.Padding = new Padding(12, 16, 12, 16);
                        ui.CornerRadius = new CornerRadius(4);
                        ui.BackgroundColor = new Vector4(0.078f, 0.078f, 0.078f, 1f);
                        ui.Border = new BorderStyle { Thickness = 1, Color = new Vector4(0.2f, 0.2f, 0.2f, 1f) };

                        using (ui.Text(item.Message)) { }
                    }
                }
            }
        }
    }
}
```

## Ahead-of-time (AOT) compilation contract

Reef markup is strictly a compile-time system:

- **Zero runtime reflection**: Emitted code compiles into strongly-typed C#
  method calls.
- **Zero runtime parsing**: Neither XML nor CSS parsers exist in the game
  runtime or release builds.
- **Native AOT ready**: Fully compatible with .NET trimming, Native AOT, and
  precompiled engine binaries.

## Source of truth

- Reef UI engine internals: [specs/reef-ui](reef-ui.md)
- `Reef/UIBuilder.cs`: Layout builder and element scopes.
- `Reef/UIController.cs`: Input interaction states, clicks, and keys.
- `Reef/UI/Style.cs`: Visual properties and borders.
- `Reef/UI/Constraints.cs`: Constraint structure and anchors.
- `Reef/UI/Padding.cs`: Padding model (LTRB order).
- `Reef/UI/CornerCapStyle.cs`: Sci-fi HUD bracket style.
- `Reef/UI/Viewport.cs`: Scrolling and clipping model.
- `Reef/UI/StyleOverride.cs`: Nullable caller style overrides.
- `Reef/UI/ConstraintsOverride.cs`: Nullable caller constraint overrides.
- `Reef/ReefComponentAttribute.cs`: Cross-assembly component metadata.
- `Reef.Generator/`: Compile-time parser, analyzer, style resolver, and emitter.
- `Reef.Generator.Tests/`: Parser, diagnostics, schema, and emission tests.
- `Reef.Markup.Tests/`: Runtime, component, interaction, and visual tests.
- `userdocs/reef/`: Onboarding, examples, quick reference, and traceability.
