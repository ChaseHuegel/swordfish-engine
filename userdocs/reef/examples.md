# Examples

These examples are complete in shape. The linked fixture files compile in the
test projects and run on every test pass.

## Hello view

```xml
<Reef xmlns="Reef"
      Class="HelloView"
      Context="HelloState"
      Namespace="MyGame.UI">
    <Element layout="vertical" width="280" padding="12" spacing="6" background="#101820">
        <Text text="Hello {context.Name}" size="18" color="#ffffff" />
        <Text text="Gold: {context.Gold}" size="14" color="#ffd166" />
    </Element>
</Reef>
```

```csharp
namespace MyGame.UI;

public sealed class HelloState
{
    public string Name { get; set; } = "Player";
    public int Gold { get; set; }
}

public partial class HelloView;
```

## Styled panel and button

```xml
<Element layout="vertical" class="panel" padding="10" spacing="8">
    <Text text="@context.Title" size="18" />
    <Element class="action-button" onclick="OnAccept">
        <Text text="Accept" />
    </Element>
</Element>
```

```css
:root {
    --surface: #151a20;
    --accent: #4fc3f7;
}

.panel {
    background: var(--surface);
    border: 1px #2a3a4a;
    corner-radius: 4px;
}

.action-button {
    padding: 6px 10px;
    background: #222222;
    color: #ffffff;
    corner-radius: 3px;
}

.action-button:hover {
    background: #333333;
}

.action-button:active {
    background: #111111;
}
```

A later class overrides an earlier class. An inline attribute overrides a
class. A pseudo state rule overrides a normal class rule.

## List with an empty state

```xml
<ForEach in="@context.Members" as="member" index="i">
    <Element id="member_{member.Id}" layout="horizontal" spacing="8"
             onclick="OnSelect(member.Id, i)">
        <Text text="#{i}: {member.Name}" />
    </Element>
    <Empty>
        <Text text="No members." class="muted" />
    </Empty>
</ForEach>
```

The `id` must contain a loop value. A static `id` in a loop is a collision.

## Nested loop

```xml
<ForEach in="@context.Groups" as="group">
    <Element layout="vertical" spacing="4">
        <Text text="Group {group.Id}" />
        <ForEach in="@group.Items" as="item" index="j">
            <Element id="item_{group.Id}_{item.Id}">
                <Text text="{item.Name} {j}" />
            </Element>
        </ForEach>
    </Element>
</ForEach>
```

An inner loop can read the outer loop value and the root context.

## Modal with content slots

Component file `ModalDialog.rml`:

```xml
<Reef xmlns="Reef"
      Class="ModalDialog"
      Context="ModalState"
      Namespace="MyGame.UI">
    <Element layout="vertical" class="modal" width="320" padding="12" spacing="8">
        <Content name="header">
            <Text text="Default title" size="18" />
        </Content>
        <Content>
            <Text text="Default body." />
        </Content>
        <Content name="actions" />
    </Element>
</Reef>
```

Call site:

```xml
<ModalDialog context="@context.Modal" width="360">
    <Content name="header">
        <Text text="@context.Modal.Title" size="20" />
    </Content>
    <Text text="This is the default slot." />
    <Content name="actions">
        <Element id="close" class="action-button" onclick="OnClose">
            <Text text="Close" />
        </Element>
    </Content>
</ModalDialog>
```

The linked fixture is
[ModalView.rml](../../Reef.Markup.Tests/Fixtures/ModalView.rml).

## Bound settings form

```xml
<Element layout="vertical" spacing="6">
    <wb:Checkbox id="mute" text="Mute"
                 value="@context.IsMuted"
                 bind="context.IsMuted" />

    <wb:Toggle id="fullscreen" on-label="On" off-label="Off"
               value="@context.Fullscreen"
               bind="context.Fullscreen" />

    <wb:RefTextBox id="chat" bind="context.Chat"
                   font-options="@context.FontOptions" />
</Element>
```

`Checkbox` returns a `bool`. `Toggle` returns a change result with `Changed`
and `Value`. `RefTextBox` takes a `ref` state value. The generator writes the
change detection and the writeback.

The linked fixture is
[WidgetsView.rml](../../Reef.Markup.Tests/Fixtures/WidgetsView.rml).

## HUD with overlay and clipping

```xml
<Element layout="none" width="100%" height="100%">
    <Element width="200" height="100" clip="true" background="#111111">
        <Element width="320" height="100" background="#e67e22" />
    </Element>

    <Element width="180" height="80" x="20" y="20" overlay="true"
             background="#00ff00" border="1 #ffffff" />
</Element>
```

`clip="true"` clips children to the element bounds. `overlay="true"` lets an
element escape ancestor clipping. Overlay elements paint above normal
elements.

## Theme override

```css
/* BaseTheme.rss */
:root {
    --accent: #3b82f6;
}
```

```css
/* GameTheme.rss */
:root {
    --accent: #ffaa00 !important;
    --game-health: #22c55e;
}

.health-bar {
    background: var(--game-health);
    border: 1px var(--accent);
}
```

Only one file can define `--accent` without `!important`. Only one file can
mark `--accent` with `!important`. The legacy `override --accent` form is still
accepted.

## Cross-assembly component

A component library exposes a markup component:

```xml
<Reef xmlns="Reef"
      Class="PlayerCard"
      Context="PlayerInfo"
      Namespace="MyGame.UI.Components">
    <Element layout="vertical" class="player-card" width="220" padding="8">
        <Text text="@context.Name" />
        <Text text="Score: {context.Score}" />
    </Element>
</Reef>
```

The generated class has a `ReefComponent` attribute. A consumer project can
use the tag directly:

```xml
<PlayerCard context="@context.Player" width="240" />
```

The consumer project must reference the component assembly and must not define
a local component with the same class name.

The linked library fixtures are
[PlayerCard.rml](../../Reef.Markup.TestComponents/Components/PlayerCard.rml) and
[ModalDialog.rml](../../Reef.Markup.TestComponents/Components/ModalDialog.rml).

## Visual gallery

The visual test renders the gallery in normal, hover, and disabled states.

- [Normal image](../../Reef.Markup.Tests/TestFiles/Golden/gallery-normal.png)
- [Hover image](../../Reef.Markup.Tests/TestFiles/Golden/gallery-hover.png)
- [Disabled image](../../Reef.Markup.Tests/TestFiles/Golden/gallery-disabled.png)

The gallery fixture is
[GalleryView.rml](../../Reef.Markup.Tests/Fixtures/GalleryView.rml).
