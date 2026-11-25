using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views.Dialogs;

/// <summary>
///     Confirmation dialog for removing a recent workspace entry and optionally deleting its directory on disk.
/// </summary>
public sealed partial class DeleteRecentWorkspaceDialog : UserControl
{
    /// <summary>
    ///     Initializes the recent workspace removal dialog components.
    /// </summary>
    public DeleteRecentWorkspaceDialog()
    {
        InitializeComponent();
    }
}
