# Reef.Generator

Roslyn source generator for Reef Markup (`.rml`) and Reef Style Sheets
(`.rss`).

## Install

```xml
<PackageReference Include="Reef.Generator" Version="0.1.0" PrivateAssets="all" />
```

The package adds all `.rml` and `.rss` files as Roslyn `AdditionalFiles`. It
also includes `schemas/reef.xsd` for editor completion.

## Use

```xml
<Reef xmlns="Reef" Class="HelloView" Context="HelloState" Namespace="MyGame.UI">
    <Element layout="vertical" width="240" padding="8" background="#101820">
        <Text text="Hello {context.Name}" />
    </Element>
</Reef>
```

The generator writes the `HelloView` partial class and its `Render` method at
build time. The runtime does not parse XML or CSS.

See the repository `userdocs/reef/` folder for the full guide.
