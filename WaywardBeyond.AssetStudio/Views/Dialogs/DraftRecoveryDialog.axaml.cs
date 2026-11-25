using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views.Dialogs;

/// <summary>
///     Modal dialog offering per-file recover or discard choices for unclean-exit drafts.
/// </summary>
public sealed partial class DraftRecoveryDialog : UserControl
{
    /// <summary>
    ///     Initializes the draft recovery dialog.
    /// </summary>
    public DraftRecoveryDialog()
    {
        InitializeComponent();
    }
}
