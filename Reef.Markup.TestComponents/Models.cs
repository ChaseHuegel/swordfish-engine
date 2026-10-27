namespace Reef.Markup.TestComponents;

/// <summary>Context used by the PlayerCard test component.</summary>
public sealed class PlayerInfo
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Id { get; set; } = string.Empty;
}

/// <summary>Context used by the ModalDialog test component.</summary>
public sealed class ModalState
{
    public string Title { get; set; } = string.Empty;
}

/// <summary>Context used by the WidgetGallery test component.</summary>
public sealed class WidgetState
{
    public bool Muted { get; set; }
    public bool Fullscreen { get; set; }
    public bool Decoupled { get; set; }
    public bool SectionOpen { get; set; } = true;
    public TextBoxState Chat = new();
    public TextBoxState Notes = new();
    public Reef.UI.FontOptions FontOptions { get; set; } = new() { Size = 14 };
}
