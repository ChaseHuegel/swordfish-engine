using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents a single entry inside either the editable file order or effective order tag pane.
/// </summary>
public sealed partial class TagListEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private string _provider;

    [ObservableProperty]
    private bool _isDuplicate;

    [ObservableProperty]
    private bool _isBaseProvider;

    /// <summary>
    ///     Initializes a tag list row with ordering index and provenance metadata.
    /// </summary>
    public TagListEntryViewModel(int index, string value, string provider, bool isDuplicate, bool isBaseProvider)
    {
        _index = index;
        _value = value;
        _provider = provider;
        _isDuplicate = isDuplicate;
        _isBaseProvider = isBaseProvider;
    }

    /// <summary>
    ///     Zero-padded display index highlighting semantic seed-phrase order.
    /// </summary>
    public string IndexLabel => $"#{Index + 1:00}";
}
