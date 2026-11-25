using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>
///     Center editor host coordinating breadcrumb, identity header, per-kind forms, and raw TOML view.
/// </summary>
public sealed partial class EditorHostView : UserControl
{
    /// <summary>
    ///     Initializes the editor host view.
    /// </summary>
    public EditorHostView()
    {
        InitializeComponent();
    }
}
