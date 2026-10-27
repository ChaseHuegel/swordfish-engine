namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Editable values for the add-favorite modal.</summary>
internal sealed class AddFavoriteState(TextBoxState host, TextBoxState port)
{
    /// <summary>The host field.</summary>
    public TextBoxState Host = host;

    /// <summary>The port field.</summary>
    public TextBoxState Port = port;

    /// <summary>The current validation message key, or null.</summary>
    public string? ErrorKey;

    /// <summary>Gets whether the state has a validation message.</summary>
    public bool HasError => ErrorKey != null;
}
