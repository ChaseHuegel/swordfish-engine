# Quick Reference

## Root element

```xml
<Reef xmlns="Reef"
      Class="ViewName"
      Context="StateType"
      RendererData="Swordfish.Graphics.Material"
      Namespace="MyGame.UI">
</Reef>
```

| Attribute | Required | Meaning |
|---|---|---|
| `Class` | Yes | Generated partial class name. |
| `Context` | No | Context type passed to `Render`. |
| `RendererData` | No | Closes `UIBuilder<TData>` to a concrete type. |
| `Namespace` | No | Generated namespace. Defaults to the root namespace and folder path. |
| `Access` | No | `public` or `internal`. Defaults to the companion class accessibility, then `public`. |

## Tags

| Tag | Purpose |
|---|---|
| `Element` | Container and layout primitive. |
| `Text` | Text with font, alignment, and wrapping options. |
| `Image` | Texture or material quad. |
| `If` | First conditional branch. |
| `ElseIf` | Later conditional branch. |
| `Else` | Final conditional branch. |
| `ForEach` | Repeats children over a collection. |
| `Empty` | Content for an empty `ForEach`. |
| `Content` | Named or default content slot. |
| `PlayerCard` | A local or referenced component. The tag matches `Class`. |
| `wb:Widget` | An external C# widget from `xmlns:wb`. |

## Element attributes

| Attribute | Values |
|---|---|
| `layout` | `vertical`, `horizontal`, `none` |
| `spacing` | Integer pixels |
| `id` | Stable string. Use `{loopVar}` inside loops. |
| `onclick` | View or context callback |
| `onrightclick` | Right-click callback |
| `onmiddleclick` | Middle-click callback |
| `onheld` | Callback on each held frame |
| `button` | `left`, `right`, `middle` |
| `hotkey` | `Esc`, a context expression, or a helper call |
| `key` | Alias for `hotkey` |
| `disabled` | `true`, `false`, or a Boolean expression |
| `passthrough` | `true` or `false` |
| `overlay` | `true` or `false` |
| `visible` | `true`, `false`, or a Boolean expression |
| `scroll` | `none`, `vertical`, `horizontal`, `both` |
| `scroll-x` | Integer or expression |
| `scroll-y` | Integer or expression |
| `onscroll` | Scroll callback |
| `clip` | `true`, `false`, or `"width height"` |
| `clip-width` | Integer or expression |
| `clip-height` | Integer or expression |

## Text attributes

| Attribute | Values |
|---|---|
| `text` | Literal, interpolation, or expression |
| `size` | Font size in pixels. Default 16. |
| `font` | Font asset ID |
| `text-align` / `align` | `left`, `center`, `right` |
| `vertical-align` / `valign` | `top`, `middle`, `bottom` (CSS form) or `center` |
| `wrap` | `true` or `false` |

Text body content is also valid:

```xml
<Text>Hello</Text>
<Text xml:space="preserve">  keep spaces  </Text>
```

The `text` attribute wins when both forms are present.

## Image attributes

| Attribute | Meaning |
|---|---|
| `source` | Expression that returns the `RendererData` type |
| `color` | Tint color |
| `background` | Color behind the image |

`<Image>` requires `RendererData`.

## Layout values

| Value | Meaning |
|---|---|
| `100` or `100px` | Fixed pixels |
| `50%` or `0.5` | Relative to the parent |
| `fill` | Uses the remaining space |
| `fit` | Fits the children |
| `shrink` | Fits the children and allows compression |

| Attribute | Meaning |
|---|---|
| `width`, `height` | Axis constraints |
| `size` | Both axes. On `<Text>`, `size` means font size. |
| `min-width`, `min-height`, `min-size` | Minimum bounds |
| `max-width`, `max-height`, `max-size` | Maximum bounds |
| `x`, `y` | Position offsets |
| `pos`, `position` | `x y` offset pair |
| `anchor`, `anchors` | `Top`, `Left`, `Bottom`, `Right`, `Center`, `Local` |
| `padding` | See the padding order below |
| `spacing` | Gap between children along the layout axis |

### Padding order

| Values | Result |
|---|---|
| `10` | Left, top, right, bottom all 10 |
| `10 20` | Horizontal 10, vertical 20 |
| `10 20 30 40` | Left 10, top 20, right 30, bottom 40 |

### Corner radius order

The markup order is CSS order:

```xml
<Element corner-radius="4 8 12 16" />
```

This sets top-left 4, top-right 8, bottom-right 12, and bottom-left 16. The
generator maps the values into the Reef constructor order.

## Colors

| Form | Example |
|---|---|
| 3-digit hex | `#fff` |
| 4-digit hex | `#fff8` |
| 6-digit hex | `#ffffff` |
| 8-digit hex | `#ffffff80` |
| Named color | `rebeccapurple` |
| RGB function | `rgb(0, 128, 255)` |
| RGBA function | `rgba(255, 255, 0, 0.5)` |
| Variable | `var(--accent)` |

Named colors use the CSS color names. `transparent` is supported.

## Stylesheet properties

### Visual

`color`, `text-color`, `background`, `background-color`, `corner-radius`,
`border-radius`, `border`, `border-width`, `border-color`, `border-top`,
`border-right`, `border-bottom`, `border-left`, `overlay`, `corner-caps`,
`corner-caps-length`, `corner-caps-thickness`, `corner-caps-color`

The aliases `background-color` and `border-radius` map to `background` and
`corner-radius`. `text-color` inherits into descendant text. Use `border-bottom`
and the other side borders for attached tabs and underlines.

Use `px` units for integer values such as `padding: 8px 16px`,
`corner-radius: 4px`, `border: 1px #444444`, and `font-size: 16px`. CSS editors
then accept the file.

### Typography

`font-size`, `font-family` (preferred for CSS tools), `font`, `text-align`,
`vertical-align`, `white-space` (`normal`, `nowrap`)

### Dimensions and constraints

`width`, `height`, `size`, `min-width`, `min-height`, `min-size`, `max-width`,
`max-height`, `max-size`, `pos`, `x`, `y`, `anchor`, `anchors`

### Layout

`padding`, `spacing`, `gap`, `layout`

The alias `gap` maps to `spacing`.

## Style selectors

```css
:root {
    --accent: #4fc3f7;
}

.base {
    background: #101820;
}

.panel {
    --extends: base;
    border: 1px #4fc3f7;
}

.panel:hover {
    background: #1a2630;
}

.panel:active {
    background: #0d1418;
}

.panel:focus {
    border-color: #ffffff;
}

.panel:disabled {
    background: #222222;
}
```

Inheritance uses `--extends: base;` (multiple base classes are supported: `--extends: base1 base2;`). Variable overrides use `!important`.
The legacy `.sub : .base` and `override --name` forms are still accepted.
Pseudo states are `:hover`, `:active`, `:focus`, and `:disabled`.

## Precedence

The final value for a property resolves in this order. A later item wins:

1. Base class rule.
2. Inherited class rule.
3. Sibling classes in attribute order.
4. Dynamic class directive that is true.
5. Pseudo state rule.
6. Inline markup attribute.
7. Caller style override on a component.

Pseudo state priority is `disabled`, then `active`, then `focus`, then `hover`.

## Expressions

| Form | Example |
|---|---|
| Context property | `@context.Name` |
| Context method | `@context.CalculateScore(context.Streak, ui.Time)` |
| View helper | `@Localize('ui.menu.title')` |
| UI metric | `@ui.Time`, `@ui.Delta`, `@ui.Width` |
| UI method | `@ui.GetElementBounds('card').Rect.Size.X` |
| Interpolation | `"Level {context.Level}"` |
| Literal braces | `"{{escaped}}"` |

### Comparison aliases

| Alias | C# operator | XML entity |
|---|---|---|
| `lt` | `<` | `&lt;` |
| `lte` | `<=` | `&lt;=` |
| `gt` | `>` | `&gt;` |
| `gte` | `>=` | `&gt;=` |
| `eq` | `==` | `==` |
| `ne` | `!=` | `!=` |

Example:

```xml
<If condition="@context.Gold lt context.Cost">
    <Text text="Not enough gold." />
</If>
```

### Helper return types

The generator infers a helper return type from the attribute:

| Attribute | Expected type |
|---|---|
| `text` | `string` |
| `color` | `Vector4` |
| `condition`, `visible`, `disabled` | `bool` |
| `width`, `height` | Integer constraint |
| `hotkey` | `UIController.Key` |
| `source` | `RendererData` |

A non-void helper becomes a `protected partial` method. A callback becomes a
`partial void` method.

## Two-way binding

```xml
<wb:Checkbox id="mute" text="Mute"
             value="@context.IsMuted"
             bind="context.IsMuted" />
```

The generator supports three bind shapes:

| Widget shape | Behavior |
|---|---|
| Returns `bool` or the target type | Compare with the target, then write back. |
| Returns `ControlResult<T>` | Use `Changed` and `Value`. |
| Takes `ref T` | Pass the target field by reference. |

The target context must be a reference type. A read-only member is a compile
error. Use `value` and `onchange` when validation must happen before writeback.

## Callbacks

```xml
<Element onclick="OnStart" />
<Element onclick="OnSelectTab('audio')" />
<Element onclick="OnKick(member.Id)" />
<Element onclick="OnApply('volume', context.Volume, ui.Delta)" />
<Element scroll="vertical" onscroll="OnScroll(tab.Id, ui.GetScrollDelta())" />
```

A callback without `@context.` targets the view code-behind. A callback with
`@context.` calls the context method.

## Hotkeys

```xml
<Element hotkey="Esc" onclick="OnQuit" />
<Element hotkey="@context.InventoryKey" onclick="OnInventory" />
<Element hotkey="@GetKeyBinding('gameplay.interact')" onclick="OnInteract" />
```

The last form generates:

```csharp
protected partial UIController.Key GetKeyBinding(string action);
```

## Element IDs

- Use `id` for explicit stable identity.
- An interactive element without `id` gets a generated ID from the file, line,
  and column.
- An interactive element inside a loop must use an `id` with a loop value,
  such as `id="item_{item.Id}"`.
- A component call uses its `id` as the instance prefix for inner generated
  IDs.

## Component caller overrides

A generated component `Render` method accepts these optional parameters:

| Parameter | Type | Meaning |
|---|---|---|
| `callerStyle` | `StyleOverride?` | Nullable visual overrides. |
| `callerConstraints` | `ConstraintsOverride?` | Nullable layout overrides. |
| `idPrefix` | `string?` | Prefix for inner generated element IDs. |

`StyleOverride` fields: `Color`, `BackgroundColor`, `Padding`,
`CornerRadius`, `Border`, and `CornerCaps`.

`ConstraintsOverride` fields: `Anchors`, `X`, `Y`, `Width`, `Height`,
`MinWidth`, `MinHeight`, `MaxWidth`, and `MaxHeight`.

## Diagnostics

| ID | Meaning |
|---|---|
| `RF0001` | XML syntax error |
| `RF0002` | Style syntax or variable error |
| `RF0003` | Style inheritance error |
| `RF0004` | Unresolved expression |
| `RF0005` | Invalid constraint |
| `RF0006` | Unused content slot |
| `RF0007` | Unresolved XML namespace |
| `RF0008` | Invalid conditional order |
| `RF0009` | Invalid `Empty` tag |
| `RF0010` | Missing key-bound ID in a loop |
| `RF0011` | Invalid bind target |
| `RF0012` | Duplicate style variable |
| `RF0013` | Conflicting style override |
| `RF0014` | Void or value widget with children |
| `RF0015` | Unknown element or component |
| `RF0016` | Invalid color |
| `RF0017` | Invalid dimension |
| `RF0018` | Invalid shorthand |
| `RF0019` | Unknown or invalid attribute |
| `RF0020` | Invalid expression |
| `RF0021` | Type mismatch |
| `RF0022` | Unknown style property |
| `RF0023` | Invalid enum value |
| `RF0024` | Duplicate element ID warning |
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

## Compile-time contract

- The generator emits strongly typed C# method calls.
- The runtime contains no XML parser and no CSS parser.
- The generated code uses no reflection.
- Native AOT and trimming are supported.
- `reef.xsd` gives editor completion and validation.
