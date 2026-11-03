# Rider Setup

This guide shows how to make Rider useful for `.rml` and `.rss` files.

## File types

Map the extensions to the closest Rider language:

1. Open **Settings | Editor | File Types**.
2. Select **XML** and add the pattern `*.rml`.
3. Select **CSS** and add the pattern `*.rss`.

Rider now highlights XML tags and CSS rules. The generator still performs the
authoritative validation during the build.

## Register the Reef schema

The schema is at `Reef.Generator/Schemas/reef.xsd`.

Add the schema to the project resource list:

1. Open **Settings | Languages & Frameworks | Schemas and DTDs**.
2. In **External Schemas and DTDs**, add this mapping:

   | URI | Schema file |
   |---|---|
   | `Reef` | `Reef.Generator/Schemas/reef.xsd` |

3. Apply the setting.

You can also add `Reef.Generator/Schemas/catalog.xml` under **XML Catalogs**. The
catalog maps `Reef` to `reef.xsd` without a manual URI entry.

Alternatively, add a schema location to each root element:

```xml
<Reef xmlns="Reef"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:schemaLocation="Reef Reef.Generator/Schemas/reef.xsd"
      Class="WidgetsView">
```

The URI must be exactly `Reef`. The default XML namespace must also be `Reef`.

## Ignore external widget namespaces

A prefix such as `xmlns:wb="Reef.Markup.TestComponents"` points to a C# namespace,
not an XML schema. Rider reports **URI is not registered** for that namespace.
This is an editor warning, not a markup error.

Use one of these fixes:

1. Put the cursor on the namespace value.
2. Press `Alt+Enter`.
3. Select **Ignore external resource**.

Or add the value to the ignored list:

1. Open **Settings | Languages & Frameworks | Schemas and DTDs**.
2. Add `Reef.Markup.TestComponents` to **Ignored Schemas and DTDs**.

Add `Reef.Class` to the same ignored list when you use `class:active` or another
dynamic class directive:

```xml
<Reef xmlns="Reef"
      xmlns:class="Reef.Class"
      Class="TabView">
```

The generator validates widget tags and attributes. Rider cannot give exact
completion for an open set of C# widget namespaces.

## Make `.rss` CSS-compatible

The generator accepts the legacy Reef style syntax. Rider CSS inspection does
not. Use these CSS-compatible forms in new style sheets.

### Class inheritance

Use a custom property:

```css
.base {
    background: #101820;
    border: 1px #4fc3f7;
}

.panel {
    --extends: base;
    padding: 8px;
}
```

The legacy form `.panel : .base` is still accepted by the generator. It is not
valid CSS.

### Variable override

Use `!important`:

```css
:root {
    --accent: #ffaa00 !important;
}
```

The legacy form `override --accent: #ffaa00;` is still accepted. It is not
valid CSS.

### Units

Use `px` for integer properties:

```css
padding: 8px 16px;
border: 1px #444444;
corner-radius: 4px;
font-size: 16px;
```

The generator also accepts unitless integers. CSS editors do not.

### Layout keywords

A known CSS property rejects Reef layout keywords such as `fill`, `fit`, and
`shrink`. Use a custom property value when the style sheet needs those values:

```css
:root {
    --fill: fill;
    --fit: fit;
    --shrink: shrink;
}

.grow {
    width: var(--fill);
}
```

Use `middle` for a centered vertical alignment. The generator also accepts
`center`:

```css
vertical-align: middle;
```

Use `font-family` for a font ID. Quote the ID if it contains spaces:

```css
font-family: "salmon-mono-9-regular";
```

### Unknown properties

Rider may report **Unknown property** for Reef-specific properties such as
`corner-radius`, `corner-caps`, `layout`, `spacing`, `anchor`, and `size`.
These are valid Reef properties and valid CSS syntax. Disable the CSS property
inspection for this project, or ignore the warnings.

## Custom component tags

A tag such as `<PlayerCard>` is a local or referenced component. Rider cannot
validate its attributes without a generated project schema. The generator checks
the tag and attributes during the build. Keep the XML file well formed and use
the Reef diagnostics for the real validation.

## Check the result

1. Build the project.
2. Open the **Build** tool window.
3. Look for `RF` diagnostics.

A clean build and no `RF` diagnostics mean the markup and style sheets are
valid. Editor warnings about open widget namespaces or unknown CSS properties
do not change the generated output.
