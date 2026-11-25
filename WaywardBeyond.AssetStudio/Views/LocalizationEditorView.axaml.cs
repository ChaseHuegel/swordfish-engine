using Avalonia.Controls;
using Avalonia.Input;
using WaywardBeyond.AssetStudio.ViewModels;

namespace WaywardBeyond.AssetStudio.Views;

/// <summary>
///     Per-file key-value localization table editor with locale selector and live filtering.
/// </summary>
public sealed partial class LocalizationEditorView : UserControl
{
    /// <summary>
    ///     Initializes the localization table editor view.
    /// </summary>
    public LocalizationEditorView()
    {
        InitializeComponent();
    }

    private void HandleNewKeyInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && DataContext is AssetDocumentViewModel doc
            && !doc.IsReadOnly)
        {
            doc.AddLocalizationRowCommand.Execute(null);
            e.Handled = true;
        }
    }
}
