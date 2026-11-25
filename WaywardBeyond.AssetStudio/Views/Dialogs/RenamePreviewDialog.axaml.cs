using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views.Dialogs;

/// <summary>
///     Modal dialog previewing affected files and spans for an explicit cross-file ID rename.
/// </summary>
public sealed partial class RenamePreviewDialog : UserControl
{
    /// <summary>
    ///     Initializes the rename preview dialog.
    /// </summary>
    public RenamePreviewDialog()
    {
        InitializeComponent();
    }
}
