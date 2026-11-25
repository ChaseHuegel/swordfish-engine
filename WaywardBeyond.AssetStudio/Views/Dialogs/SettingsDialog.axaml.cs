using Avalonia.Controls;

namespace WaywardBeyond.AssetStudio.Views.Dialogs;

/// <summary>
///     Modal dialog for editing studio theme, culture override, GL preference, log level, and layout.
/// </summary>
public sealed partial class SettingsDialog : UserControl
{
    /// <summary>
    ///     Initializes the settings dialog.
    /// </summary>
    public SettingsDialog()
    {
        InitializeComponent();
    }
}
