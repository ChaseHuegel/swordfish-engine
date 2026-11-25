using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Selectable toggle chip view model used for filter bars and state selectors.
/// </summary>
public sealed partial class FilterChipViewModel : ObservableObject
{
    private readonly Action<FilterChipViewModel> _onSelected;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    ///     Initializes a filter chip with label, initial active state, and selection callback.
    /// </summary>
    public FilterChipViewModel(string label, bool isSelected, Action<FilterChipViewModel> onSelected)
    {
        Label = label;
        _isSelected = isSelected;
        _onSelected = onSelected;
    }

    /// <summary>
    ///     Display text rendered inside the chip button.
    /// </summary>
    public string Label { get; }

    [RelayCommand]
    private void Select()
    {
        _onSelected(this);
    }
}
