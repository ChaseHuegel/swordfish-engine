namespace Reef.Markup.Tests;

public sealed class FlowItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class FlowGroup
{
    public string Id { get; set; } = string.Empty;
    public List<FlowItem> Items { get; set; } = [];
}

public sealed class FlowContext
{
    public bool Loading { get; set; }
    public bool Error { get; set; }
    public bool ShowFooter { get; set; } = true;
    public bool Locked { get; set; }
    public int Gold { get; set; }
    public string Tab { get; set; } = "ready";
    public List<FlowItem> Items { get; set; } = [];
    public List<FlowGroup> Groups { get; set; } = [];
    public bool IsTabActive(string tab) => Tab == tab;
}

public sealed class TextContext
{
    public int Level { get; set; } = 4;
    public int Gold { get; set; } = 120;
    public string Value { get; set; } = "x";
    public string LongText { get; set; } = "A long piece of text that must wrap.";
}

public sealed class StyleContext
{
    public bool Disabled { get; set; }
    public bool Accent { get; set; }
}

public sealed class OverlayContext
{
    public bool Hide { get; set; }
    public int ScrollY { get; set; }
}

public sealed class UiContext
{
    public int Cooldown { get; set; } = 5;
    public int Streak { get; set; } = 3;
    public int Rarity { get; set; } = 2;
    public Reef.PixelTexture Icon { get; set; } = new(1, 1);

    public string CalculateScore(int streak, double time) => $"Score {streak} at {time:0.0}";
}

public sealed class PointerContext
{
    public bool Disabled { get; set; }
}

public sealed class InstanceContext
{
    public Reef.Markup.TestComponents.CaptionState State { get; } = new() { Prefix = "Hello", Name = "World" };
    public Reef.Markup.TestComponents.CaptionedCard Card { get; } = new("Hello");
    public Reef.Markup.TestComponents.ModalState ModalA { get; } = new() { Title = "A" };
    public Reef.Markup.TestComponents.ModalState ModalB { get; } = new() { Title = "B" };
}

public sealed class PlayerListContext
{
    public List<Reef.Markup.TestComponents.PlayerInfo> Players { get; } =
    [
        new Reef.Markup.TestComponents.PlayerInfo { Id = "a", Name = "Ada", Score = 10 },
        new Reef.Markup.TestComponents.PlayerInfo { Id = "b", Name = "Bob", Score = 20 },
    ];
}
