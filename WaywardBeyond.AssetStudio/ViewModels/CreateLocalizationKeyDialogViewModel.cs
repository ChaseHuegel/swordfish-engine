using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the create localization key dialog triggered from localization pickers.
/// </summary>
public sealed partial class CreateLocalizationKeyDialogViewModel : ObservableObject
{
    private readonly Action<string, string, string> _onCreate;
    private readonly Action _onClose;
    private string _keyName;
    private string _targetFile;
    private string _initialText;

    /// <summary>
    ///     Initializes the create localization key dialog with default target file and key suggestion.
    /// </summary>
    public CreateLocalizationKeyDialogViewModel(
        string suggestedKey,
        string defaultTargetFile,
        IReadOnlyList<string> targetFiles,
        Action<string, string, string> onCreate,
        Action onClose)
    {
        _keyName = suggestedKey ?? string.Empty;
        _targetFile = defaultTargetFile ?? string.Empty;
        _initialText = "Ion Thruster Mk.II";
        _onCreate = onCreate;
        _onClose = onClose;

        for (var i = 0; i < targetFiles.Count; i++)
        {
            TargetFiles.Add(targetFiles[i]);
        }
    }

    /// <summary>
    ///     Dot-separated localization key to create.
    /// </summary>
    public string KeyName
    {
        get => _keyName;
        set => SetProperty(ref _keyName, value ?? string.Empty);
    }

    /// <summary>
    ///     Target localization TOML file receiving the new key.
    /// </summary>
    public string TargetFile
    {
        get => _targetFile;
        set => SetProperty(ref _targetFile, value ?? string.Empty);
    }

    /// <summary>
    ///     Initial English translation text written for the new key.
    /// </summary>
    public string InitialText
    {
        get => _initialText;
        set => SetProperty(ref _initialText, value ?? string.Empty);
    }

    /// <summary>
    ///     Selectable target localization TOML files in the workspace.
    /// </summary>
    public ObservableCollection<string> TargetFiles { get; } = [];

    [RelayCommand]
    private void CreateKey()
    {
        if (string.IsNullOrWhiteSpace(KeyName) || string.IsNullOrWhiteSpace(TargetFile))
        {
            return;
        }

        _onCreate(KeyName.Trim(), TargetFile, InitialText.Trim());
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }
}
