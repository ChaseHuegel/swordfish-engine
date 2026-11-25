using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views.Dialogs;

/// <summary>
///     Modal confirmation dialog displaying target file details and inbound reference warnings before deleting an asset.
/// </summary>
public sealed partial class DeleteAssetDialog : UserControl
{
    /// <summary>
    ///     Initializes the delete asset confirmation dialog view.
    /// </summary>
    public DeleteAssetDialog()
    {
        InitializeComponent();
    }
}
