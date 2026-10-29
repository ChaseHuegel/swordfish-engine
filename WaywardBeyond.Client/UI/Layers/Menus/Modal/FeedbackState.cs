namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Editable values for the feedback modal.</summary>
internal sealed class FeedbackState(TextBoxState contact, TextBoxState description)
{
    /// <summary>The contact field.</summary>
    public TextBoxState Contact = contact;

    /// <summary>The message field.</summary>
    public TextBoxState Description = description;

    /// <summary>Gets whether the message has content.</summary>
    public bool CanSubmit => Description.Text.Length > 0;
}
