namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared modal component.</summary>
internal sealed record ModalParameters(ModalSize Size = ModalSize.Auto)
{
    /// <summary>Gets whether the modal takes its size from the caller.</summary>
    public bool Auto => Size == ModalSize.Auto;

    /// <summary>Gets whether the modal uses the connecting preset.</summary>
    public bool Connecting => Size == ModalSize.Connecting;

    /// <summary>Gets whether the modal uses the compact preset.</summary>
    public bool Compact => Size == ModalSize.Compact;

    /// <summary>Gets whether the modal uses the form preset.</summary>
    public bool Form => Size == ModalSize.Form;

    /// <summary>Gets whether the modal uses the feedback preset.</summary>
    public bool Feedback => Size == ModalSize.Feedback;

    /// <summary>Gets whether the modal uses the playtest preset.</summary>
    public bool Playtest => Size == ModalSize.Playtest;
}
