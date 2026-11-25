using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views.Dialogs;

/// <summary>
///     Modal dialog showing tool version, environment diagnostics, and diagnostics archive export.
/// </summary>
public sealed partial class AboutDialog : UserControl
{
    /// <summary>
    ///     Initializes the about and diagnostics dialog.
    /// </summary>
    public AboutDialog()
    {
        InitializeComponent();
    }
}
