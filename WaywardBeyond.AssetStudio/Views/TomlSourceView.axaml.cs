using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>
///     Raw TOML code surface pairing syntax highlighting with direct text editing.
/// </summary>
public sealed partial class TomlSourceView : UserControl
{
    /// <summary>
    ///     Initializes the TOML source view.
    /// </summary>
    public TomlSourceView()
    {
        InitializeComponent();
    }
}
