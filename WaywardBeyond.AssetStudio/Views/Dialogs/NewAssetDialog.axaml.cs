using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views.Dialogs;

/// <summary>
///     Modal dialog for authoring a new asset in a new or existing TOML file.
/// </summary>
public sealed partial class NewAssetDialog : UserControl
{
    /// <summary>
    ///     Initializes the new asset dialog.
    /// </summary>
    public NewAssetDialog()
    {
        InitializeComponent();
    }
}
