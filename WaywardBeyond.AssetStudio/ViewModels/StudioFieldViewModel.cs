using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents an editable scalar or reference field with raw TOML token toggle support.
/// </summary>
public sealed partial class StudioFieldViewModel : ObservableObject
{
    private readonly Action<StudioFieldViewModel>? _onValueChanged;
    private readonly Action<StudioFieldViewModel, string, string>? _onValueTransition;
    private readonly Action<StudioFieldViewModel>? _onCreateLocalizationKey;
    private readonly Action<StudioFieldViewModel>? _onBrowseTextureFile;
    private readonly Action<StudioFieldViewModel>? _onBrowseMeshFile;
    private readonly IReadOnlyList<ReferenceCandidate> _allCandidates;
    private readonly bool _isNumericToken;
    private string _value;
    private string _rawTokenText;
    private string _pickerFilterText = string.Empty;
    private bool _syncing;

    [ObservableProperty]
    private bool _isRawMode;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _isHighlighted;

    [ObservableProperty]
    private bool _isDanger;

    [ObservableProperty]
    private string? _dangerMessage;

    [ObservableProperty]
    private string? _quickFixLabel;

    [ObservableProperty]
    private string? _quickFixValue;

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private bool _isPickerOpen;

    [ObservableProperty]
    private string _resolutionLabel = "Resolved";

    /// <summary>
    ///     Initializes a field view model with schema metadata and optional reference candidates.
    /// </summary>
    public StudioFieldViewModel(
        string fieldKey,
        string label,
        string grammarHint,
        string initialValue,
        bool isReadOnly = false,
        bool isBool = false,
        bool isColor = false,
        bool isNumeric = false,
        bool isInformational = false,
        bool showRawTokenToggle = true,
        IReadOnlyList<string>? choices = null,
        ReferenceKind? referenceKind = null,
        IReadOnlyList<ReferenceCandidate>? candidates = null,
        Action<StudioFieldViewModel>? onValueChanged = null,
        Action<StudioFieldViewModel>? onCreateLocalizationKey = null,
        bool? showUnreadBadge = null,
        Action<StudioFieldViewModel, string, string>? onValueTransition = null,
        Action<StudioFieldViewModel>? onBrowseTextureFile = null,
        Action<StudioFieldViewModel>? onBrowseMeshFile = null)
    {
        FieldKey = fieldKey;
        Label = label;
        GrammarHint = grammarHint;
        IsReadOnly = isReadOnly;
        IsBoolField = isBool;
        IsColorField = isColor;
        IsInformational = isInformational;
        ShowUnreadBadge = showUnreadBadge ?? isInformational;
        ShowRawTokenToggle = showRawTokenToggle;
        _isNumericToken = isNumeric;
        Choices = choices ?? [];
        ReferenceKind = referenceKind;
        _allCandidates = candidates ?? [];
        _onValueChanged = onValueChanged;
        _onValueTransition = onValueTransition;
        _onCreateLocalizationKey = onCreateLocalizationKey;
        _onBrowseTextureFile = onBrowseTextureFile;
        _onBrowseMeshFile = onBrowseMeshFile ?? (referenceKind == Models.ReferenceKind.Mesh ? onBrowseTextureFile : null);

        IsComboField = Choices.Count > 0;
        IsReferenceField = referenceKind != null;
        IsTextField = !IsBoolField && !IsColorField && !IsComboField && !IsReferenceField;
        AllowCreateLocalizationKey = referenceKind == Models.ReferenceKind.Localization;

        var safeInitial = initialValue ?? string.Empty;
        _value = safeInitial;
        _boolValue = string.Equals(safeInitial, "true", StringComparison.OrdinalIgnoreCase);
        _rawTokenText = FormatRawToken(safeInitial, isBool, isNumeric);

        RefreshFilteredCandidates();
        UpdateResolutionState();
    }

    /// <summary>
    ///     Canonical field key matching the asset TOML schema.
    /// </summary>
    public string FieldKey { get; }

    /// <summary>
    ///     Human-readable label shown on the left of the form row.
    /// </summary>
    public string Label { get; }

    /// <summary>
    ///     Compact type or reference grammar hint displayed below the label.
    /// </summary>
    public string GrammarHint { get; }

    /// <summary>
    ///     Whether a non-empty grammar hint should be rendered beneath the field label.
    /// </summary>
    public bool HasGrammarHint => !string.IsNullOrWhiteSpace(GrammarHint);

    /// <summary>
    ///     Friendly edited value of the field, defensively coerced to non-null text.
    /// </summary>
    public string Value
    {
        get => _value;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _value;
            if (!SetProperty(ref _value, safe))
            {
                return;
            }

            if (_syncing)
            {
                return;
            }

            _syncing = true;
            try
            {
                if (IsBoolField)
                {
                    BoolValue = string.Equals(safe, "true", StringComparison.OrdinalIgnoreCase);
                }

                RawTokenText = FormatRawToken(safe, IsBoolField, _isNumericToken);
                UpdateResolutionState();
            }
            finally
            {
                _syncing = false;
            }

            NotifyValueChanged(oldValue, safe);
        }
    }

    /// <summary>
    ///     Raw TOML token text shown when raw-token editing mode is enabled.
    /// </summary>
    public string RawTokenText
    {
        get => _rawTokenText;
        set
        {
            var safe = value ?? string.Empty;
            if (!SetProperty(ref _rawTokenText, safe))
            {
                return;
            }

            if (_syncing)
            {
                return;
            }

            var oldValue = _value;
            _syncing = true;
            try
            {
                var trimmed = safe.Trim();
                if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
                {
                    Value = trimmed[1..^1];
                }
                else
                {
                    Value = trimmed;
                }

                if (IsBoolField)
                {
                    BoolValue = string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase);
                }

                UpdateResolutionState();
            }
            finally
            {
                _syncing = false;
            }

            NotifyValueChanged(oldValue, _value);
        }
    }

    /// <summary>
    ///     Search text filtering the reference picker dropdown list.
    /// </summary>
    public string PickerFilterText
    {
        get => _pickerFilterText;
        set
        {
            var safe = value ?? string.Empty;
            if (SetProperty(ref _pickerFilterText, safe))
            {
                RefreshFilteredCandidates();
            }
        }
    }

    /// <summary>
    ///     Whether the field renders a standard text input when not in raw mode.
    /// </summary>
    public bool IsTextField { get; }

    /// <summary>
    ///     Whether the field renders a boolean checkbox when not in raw mode.
    /// </summary>
    public bool IsBoolField { get; }

    /// <summary>
    ///     Whether the field renders a live hex color swatch with input.
    /// </summary>
    public bool IsColorField { get; }

    /// <summary>
    ///     Whether the field renders a dropdown selector from fixed enum choices.
    /// </summary>
    public bool IsComboField { get; }

    /// <summary>
    ///     Whether the field renders a searchable workspace reference picker.
    /// </summary>
    public bool IsReferenceField { get; }

    /// <summary>
    ///     Whether this field is marked informational because no runtime reader consumes it today.
    /// </summary>
    public bool IsInformational { get; }

    /// <summary>
    ///     Whether an inline unread pill should be rendered beside the field label.
    /// </summary>
    public bool ShowUnreadBadge { get; }

    /// <summary>
    ///     Whether the raw TOML token toggle button is applicable to this field.
    /// </summary>
    public bool ShowRawTokenToggle { get; }

    /// <summary>
    ///     Whether the picker permits creating a new localization key in a target lang file.
    /// </summary>
    public bool AllowCreateLocalizationKey { get; }

    /// <summary>
    ///     Whether this reference field uses the visual thumbnail-grid texture picker.
    /// </summary>
    public bool IsTexturePickerField => ReferenceKind is Models.ReferenceKind.Texture or Models.ReferenceKind.BrickFace;

    /// <summary>
    ///     Whether this reference field uses the visual thumbnail-grid mesh picker.
    /// </summary>
    public bool IsMeshPickerField => ReferenceKind == Models.ReferenceKind.Mesh;

    /// <summary>
    ///     Whether this reference field renders a visual thumbnail-grid dropdown instead of a text list.
    /// </summary>
    public bool IsVisualPickerField => IsTexturePickerField || IsMeshPickerField;

    /// <summary>
    ///     Whether this reference field uses the standard text candidate list instead of the visual thumbnail grid.
    /// </summary>
    public bool IsStandardReferenceList => !IsVisualPickerField;

    /// <summary>
    ///     Whether the inline disk image browse button is available for this field.
    /// </summary>
    public bool AllowBrowseTextureFile => IsTexturePickerField && _onBrowseTextureFile != null;

    /// <summary>
    ///     Whether the inline disk OBJ mesh browse button is available for this field.
    /// </summary>
    public bool AllowBrowseMeshFile => IsMeshPickerField && _onBrowseMeshFile != null;

    /// <summary>
    ///     Whether any disk file browse button is available for this visual reference field.
    /// </summary>
    public bool AllowBrowseDiskFile => AllowBrowseTextureFile || AllowBrowseMeshFile;

    /// <summary>
    ///     Tooltip text shown on the inline disk file browse button.
    /// </summary>
    public string BrowseFileTooltip => IsMeshPickerField ? "Browse .obj mesh file..." : "Browse image file...";

    /// <summary>
    ///     Whether the visual thumbnail-grid picker currently has at least one matching candidate.
    /// </summary>
    public bool HasVisualCandidates => IsVisualPickerField && FilteredCandidates.Count > 0;

    /// <summary>
    ///     Whether the visual thumbnail-grid picker currently has zero matching candidates.
    /// </summary>
    public bool HasNoVisualCandidates => IsVisualPickerField && FilteredCandidates.Count == 0;

    /// <summary>
    ///     Compact count summary displayed beside the filter box in visual thumbnail-grid pickers.
    /// </summary>
    public string VisualPickerCountLabel
    {
        get
        {
            var count = FilteredCandidates.Count;
            if (!string.IsNullOrWhiteSpace(PickerFilterText) && _allCandidates.Count > 0)
            {
                return $"{count} of {_allCandidates.Count}";
            }

            if (IsMeshPickerField)
            {
                return count switch
                {
                    0 => "No meshes found in workspace",
                    1 => "1 mesh",
                    _ => $"{count} meshes",
                };
            }

            return count switch
            {
                0 => "No images found in workspace",
                1 => "1 image",
                _ => $"{count} images",
            };
        }
    }

    /// <summary>
    ///     Compact count summary alias retained for callers expecting the prior property name.
    /// </summary>
    public string FilteredCandidatesCountLabel => VisualPickerCountLabel;

    /// <summary>
    ///     Reference category for picker rows and grammar validation.
    /// </summary>
    public ReferenceKind? ReferenceKind { get; }

    /// <summary>
    ///     Fixed enum options for combo-box fields.
    /// </summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>
    ///     Whether a one-click replacement suggestion is currently available.
    /// </summary>
    public bool HasQuickFix => !string.IsNullOrWhiteSpace(QuickFixValue) && !IsReadOnly;

    /// <summary>
    ///     Whether a danger explanation banner should render beneath the control.
    /// </summary>
    public bool HasDangerMessage => IsDanger && !string.IsNullOrWhiteSpace(DangerMessage);

    /// <summary>
    ///     Filtered reference candidates currently visible in the picker popup list.
    /// </summary>
    public ObservableCollection<ReferenceCandidate> FilteredCandidates { get; } = [];

    partial void OnBoolValueChanged(bool value)
    {
        if (_syncing)
        {
            return;
        }

        var oldValue = _value;
        _syncing = true;
        try
        {
            Value = value ? "true" : "false";
            RawTokenText = Value;
        }
        finally
        {
            _syncing = false;
        }

        NotifyValueChanged(oldValue, _value);
    }

    partial void OnIsDangerChanged(bool value)
    {
        OnPropertyChanged(nameof(HasDangerMessage));
    }

    partial void OnDangerMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDangerMessage));
    }

    partial void OnQuickFixValueChanged(string? value)
    {
        OnPropertyChanged(nameof(HasQuickFix));
    }

    /// <summary>
    ///     Applies an external diagnostic state while preserving the current value intact.
    /// </summary>
    public void SetValidationState(bool isDanger, string? message, string? quickFixLabel = null, string? quickFixValue = null)
    {
        IsDanger = isDanger;
        DangerMessage = message;
        QuickFixLabel = quickFixLabel;
        QuickFixValue = quickFixValue;
        ResolutionLabel = isDanger ? "Invalid" : (IsInformational ? "Informational" : "Resolved");
    }

    /// <summary>
    ///     Restores the field value during undo or redo replay without re-emitting transition callbacks.
    /// </summary>
    public void ApplyValueForReplay(string restoredValue)
    {
        var safe = restoredValue ?? string.Empty;
        _syncing = true;
        try
        {
            SetProperty(ref _value, safe, nameof(Value));
            if (IsBoolField)
            {
                BoolValue = string.Equals(safe, "true", StringComparison.OrdinalIgnoreCase);
            }

            SetProperty(ref _rawTokenText, FormatRawToken(safe, IsBoolField, _isNumericToken), nameof(RawTokenText));
            UpdateResolutionState();
        }
        finally
        {
            _syncing = false;
        }
    }

    [RelayCommand]
    private void ToggleRawMode()
    {
        IsRawMode = !IsRawMode;
        if (IsRawMode)
        {
            IsPickerOpen = false;
        }
    }

    [RelayCommand]
    private void TogglePicker()
    {
        if (IsReadOnly)
        {
            return;
        }

        IsPickerOpen = !IsPickerOpen;
        if (IsPickerOpen)
        {
            RefreshFilteredCandidates();
        }
    }

    [RelayCommand]
    private void SelectCandidate(ReferenceCandidate candidate)
    {
        if (IsReadOnly)
        {
            return;
        }

        Value = candidate.Value;
        IsPickerOpen = false;
    }

    [RelayCommand]
    private void ApplyQuickFix()
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(QuickFixValue))
        {
            return;
        }

        Value = QuickFixValue;
        SetValidationState(false, null);
    }

    [RelayCommand]
    private void RequestCreateLocalizationKey()
    {
        if (!AllowCreateLocalizationKey || IsReadOnly)
        {
            return;
        }

        IsPickerOpen = false;
        _onCreateLocalizationKey?.Invoke(this);
    }

    [RelayCommand]
    private void BrowseTextureFile()
    {
        BrowseDiskFile();
    }

    [RelayCommand]
    private void BrowseDiskFile()
    {
        if (IsReadOnly)
        {
            return;
        }

        var handler = IsMeshPickerField ? _onBrowseMeshFile : _onBrowseTextureFile;
        if (handler == null)
        {
            return;
        }

        IsPickerOpen = false;
        handler.Invoke(this);
    }

    private void NotifyValueChanged(string oldValue, string newValue)
    {
        _onValueTransition?.Invoke(this, oldValue, newValue);
        _onValueChanged?.Invoke(this);
    }

    private void RefreshFilteredCandidates()
    {
        FilteredCandidates.Clear();
        var query = PickerFilterText.Trim();
        for (var i = 0; i < _allCandidates.Count; i++)
        {
            var item = _allCandidates[i];
            if (query.Length == 0
                || item.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.DisplayFileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Detail.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredCandidates.Add(item);
            }
        }

        OnPropertyChanged(nameof(VisualPickerCountLabel));
        OnPropertyChanged(nameof(FilteredCandidatesCountLabel));
        OnPropertyChanged(nameof(HasVisualCandidates));
        OnPropertyChanged(nameof(HasNoVisualCandidates));
    }

    private void UpdateResolutionState()
    {
        if (!IsReferenceField)
        {
            ResolutionLabel = IsInformational ? "Informational" : "Valid";
            return;
        }

        if (string.IsNullOrWhiteSpace(Value))
        {
            SetValidationState(false, null);
            ResolutionLabel = "Unset";
            return;
        }

        if (IsInformational)
        {
            SetValidationState(false, null);
            ResolutionLabel = "Informational";
            return;
        }

        if (ReferenceKind is Models.ReferenceKind.BrickFace or Models.ReferenceKind.Texture)
        {
            UpdateMultiEntryTextureResolutionState(ReferenceKind == Models.ReferenceKind.BrickFace);
            return;
        }

        for (var i = 0; i < _allCandidates.Count; i++)
        {
            var candidate = _allCandidates[i];
            if (string.Equals(candidate.Value, Value, StringComparison.Ordinal))
            {
                SetValidationState(false, null);
                ResolutionLabel = "Resolved";
                return;
            }

            if (string.Equals(candidate.Value, Value, StringComparison.OrdinalIgnoreCase))
            {
                SetValidationState(
                    true,
                    $"Case mismatch: '{Value}' differs from '{candidate.Value}' (breaks Linux).",
                    $"Use \"{candidate.Value}\"",
                    candidate.Value);
                ResolutionLabel = "Case Mismatch";
                return;
            }
        }

        if (ReferenceKind == Models.ReferenceKind.Localization)
        {
            SetValidationState(
                true,
                $"Localization key '{Value}' not found in active lang tables.",
                null,
                null);
            ResolutionLabel = "Missing";
            return;
        }

        SetValidationState(true, $"Unresolved reference '{Value}' (value preserved unchanged).");
        ResolutionLabel = "Missing";
    }

    private void UpdateMultiEntryTextureResolutionState(bool isBrickFace)
    {
        var tokens = Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            SetValidationState(false, null);
            ResolutionLabel = "Unset";
            return;
        }

        string? firstCaseMismatchToken = null;
        string? firstCaseMismatchTarget = null;
        string[]? fixedTokens = null;

        for (var t = 0; t < tokens.Length; t++)
        {
            var token = tokens[t];
            var match = MatchTextureCandidate(token, isBrickFace, out var matchedValue);
            if (match == TokenMatchKind.Exact)
            {
                continue;
            }

            if (match == TokenMatchKind.CaseMismatch && matchedValue != null)
            {
                firstCaseMismatchToken ??= token;
                firstCaseMismatchTarget ??= matchedValue;
                fixedTokens ??= [.. tokens];
                fixedTokens[t] = matchedValue;
                continue;
            }

            if (isBrickFace)
            {
                SetValidationState(
                    true,
                    $"Missing layer '{token}' in block textures (kept intact; runtime falls back to layer 0).",
                    "Use \"vent\"",
                    "vent");
                ResolutionLabel = "Missing";
                return;
            }

            SetValidationState(true, $"Unresolved reference '{token}' (value preserved unchanged).");
            ResolutionLabel = "Missing";
            return;
        }

        if (firstCaseMismatchToken != null && firstCaseMismatchTarget != null && fixedTokens != null)
        {
            var replacement = tokens.Length == 1
                ? firstCaseMismatchTarget
                : string.Join(", ", fixedTokens);
            SetValidationState(
                true,
                $"Case mismatch: '{firstCaseMismatchToken}' differs from '{firstCaseMismatchTarget}' (breaks Linux).",
                $"Use \"{replacement}\"",
                replacement);
            ResolutionLabel = "Case Mismatch";
            return;
        }

        SetValidationState(false, null);
        ResolutionLabel = "Resolved";
    }

    private TokenMatchKind MatchTextureCandidate(string token, bool isBrickFace, out string? canonicalValue)
    {
        canonicalValue = null;
        string? connectedVariant = isBrickFace && !token.EndsWith(".0", StringComparison.Ordinal)
            ? $"{token}.0"
            : null;

        string? caseMismatch = null;
        for (var i = 0; i < _allCandidates.Count; i++)
        {
            var candidateValue = _allCandidates[i].Value;
            if (string.Equals(candidateValue, token, StringComparison.Ordinal))
            {
                canonicalValue = candidateValue;
                return TokenMatchKind.Exact;
            }

            if (connectedVariant != null && string.Equals(candidateValue, connectedVariant, StringComparison.Ordinal))
            {
                canonicalValue = token;
                return TokenMatchKind.Exact;
            }

            if (caseMismatch == null)
            {
                if (string.Equals(candidateValue, token, StringComparison.OrdinalIgnoreCase))
                {
                    caseMismatch = candidateValue;
                }
                else if (connectedVariant != null
                    && string.Equals(candidateValue, connectedVariant, StringComparison.OrdinalIgnoreCase)
                    && candidateValue.Length > 2)
                {
                    caseMismatch = candidateValue[..^2];
                }
            }
        }

        if (caseMismatch != null)
        {
            canonicalValue = caseMismatch;
            return TokenMatchKind.CaseMismatch;
        }

        return TokenMatchKind.Missing;
    }

    private enum TokenMatchKind
    {
        Exact,
        CaseMismatch,
        Missing,
    }

    private static string FormatRawToken(string rawValue, bool isBool, bool isNumeric)
    {
        if (isBool || isNumeric)
        {
            return rawValue;
        }

        return $"\"{rawValue}\"";
    }
}
