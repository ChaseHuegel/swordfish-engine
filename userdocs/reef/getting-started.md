# Getting Started

This guide shows how to add Reef Markup to a project and render the first view.

## Requirements

- .NET 8 SDK or later.
- A project that references the Reef runtime.
- The `Reef.Generator` analyzer.

## Add the generator

For a project in this repository, add the runtime reference, the analyzer
reference, and the props import:

```xml
<ItemGroup>
    <ProjectReference Include="..\Reef\Reef.csproj" />
    <ProjectReference Include="..\Reef.Generator\Reef.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
</ItemGroup>

<Import Project="..\Reef.Generator\build\Reef.Generator.props" />
```

For a packaged project, add the analyzer package:

```xml
<PackageReference Include="Reef.Generator" Version="0.1.0" PrivateAssets="all" />
```

The package includes `build/Reef.Generator.props`. The props file adds all
`.rml` and `.rss` files as Roslyn `AdditionalFiles`. Restore the project and
open the project again if your IDE does not see the new files.

## Add the XML schema

The schema is at `Reef.Generator/Schemas/reef.xsd`. A packaged project gets it
under `schemas/reef.xsd`.

Copy the schema to the project, or add an IDE schema association. Then add the
schema location to each root element when your editor needs it:

```xml
<Reef xmlns="Reef"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:schemaLocation="Reef reef.xsd"
      Class="HelloView">
```

The schema gives tag completion and attribute validation. The generator also
checks the same rules during the build. XSD 1.0 cannot validate an unprefixed
local component directly under `<ForEach>`. Wrap it in an `<Element>` when the
schema rejects it. The generator accepts the direct form.

## Write the first view

Create `HelloView.rml`:

```xml
<Reef xmlns="Reef"
      Class="HelloView"
      Context="HelloState"
      Namespace="MyGame.UI">

    <Element layout="vertical" width="280" padding="12" spacing="6"
             background="#101820" corner-radius="4">

        <Text text="Hello {context.Name}" size="18" color="#ffffff" />
        <Text text="Gold: {context.Gold}" size="14" color="#ffd166" />

        <Element class="hello-button" onclick="OnClicked">
            <Text text="Continue" />
        </Element>
    </Element>

</Reef>
```

## Add the context and the view class

Create `HelloView.cs` in the same namespace:

```csharp
namespace MyGame.UI;

public sealed class HelloState
{
    public string Name { get; set; } = "Player";
    public int Gold { get; set; } = 25;
}

public partial class HelloView
{
    partial void OnClicked()
    {
        //  Handle the click.
    }
}
```

The generator writes the second part of `HelloView`. The partial callback
declaration is in the generated file. A root context class is the usual
choice for mutable state.

## Add a style sheet

Create `HelloTheme.rss`:

```css
:root {
    --surface: #101820;
    --accent: #4fc3f7;
}

.hello-button {
    padding: 6px 10px;
    background: #263238;
    color: #ffffff;
    corner-radius: 3px;
    border: 1px #4fc3f7;
}

.hello-button:hover {
    background: #37474f;
}

.hello-button:active {
    background: #1c262b;
}
```

All `.rss` files in the project share one variable table and one class table.
Use `px` units for integer properties. Use `--extends` for class inheritance
and `!important` for a variable override. These forms are valid CSS.

## Render the view

```csharp
var view = new HelloView();
var state = new HelloState { Name = "Ada", Gold = 120 };

view.Render(ui, state);
List<RenderCommand<Material>> commands = ui.Build(delta);
```

The generated signature is:

```csharp
public void Render(
    UIBuilder<Material> ui,
    HelloState context,
    StyleOverride? callerStyle = null,
    ConstraintsOverride? callerConstraints = null,
    string? idPrefix = null)
```

When `RendererData` is omitted, the method is generic:

```csharp
public void Render<TData>(
    UIBuilder<TData> ui,
    HelloState context,
    StyleOverride? callerStyle = null,
    ConstraintsOverride? callerConstraints = null,
    string? idPrefix = null)
```

## Use a component

Create `PlayerCard.rml`:

```xml
<Reef xmlns="Reef"
      Class="PlayerCard"
      Context="PlayerInfo"
      Namespace="MyGame.UI">

    <Element layout="vertical" class="player-card" width="220" padding="8" spacing="2">
        <Text text="@context.Name" size="16" />
        <Text text="Score: {context.Score}" size="12" />
    </Element>

</Reef>
```

Call the component from another view:

```xml
<PlayerCard context="@context.Player" width="240" class="player-highlight" />
```

The generator inlines a local component when both files are in the same
compilation. A component in a referenced assembly is called through its
generated `Render` method. In that case the component class needs a public
parameterless constructor or an `instance` attribute:

```xml
<PlayerCard context="@context.Player" instance="@context.PlayerCard" />
```

## Use an external widget

Map a C# namespace to an XML prefix:

```xml
<Reef xmlns="Reef"
      xmlns:wb="MyGame.UI.Widgets"
      Class="SettingsView"
      Context="SettingsState"
      RendererData="Swordfish.Graphics.Material">
    <wb:Checkbox id="mute" text="Mute"
                 value="@context.IsMuted"
                 bind="context.IsMuted" />
</Reef>
```

The generator matches attributes to the method parameters. It supports
`Style`, `Constraints`, and `FontOptions` parameters. Other widgets receive a
style wrapper when they do not declare those parameters.

## Use dynamic classes

Add the class namespace to the root:

```xml
<Reef xmlns="Reef"
      xmlns:class="Reef.Class"
      Class="TabView">
    <Element id="audio_tab" class="tab"
             class:active="@context.IsTabActive('audio')"
             onclick="SelectTab('audio')">
        <Text text="Audio" />
    </Element>
</Reef>
```

XML 1.0 requires a declared prefix for `class:active`. The generator uses the
`Reef.Class` namespace URI, not the prefix name. You can use a different prefix
if you want.

## Build and check

```bash
dotnet build
```

A compiler error with an `RF` identifier comes from the generator. Open the
`.rml` or `.rss` file at the reported location.

Common checks:

- Add `xmlns="Reef"` to the root.
- Use `bind`, not `@bind`. XML 1.0 does not allow `@` in attribute names.
- Add `xmlns:class="Reef.Class"` before you use `class:name`.
- Give each interactive element inside a loop a key-bound `id`.
- Add `RendererData` before you use `<Image>`.
- Use a prefixed XML namespace for external C# widgets.

## Hot Reload

The generator runs in memory. A saved `.rml` or `.rss` file produces new C#
source. .NET Hot Reload can replace the `Render` method body. The next Reef
frame uses the new layout. A change to the generated method signature or the
companion class needs a normal rebuild.
