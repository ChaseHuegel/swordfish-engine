using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Globalization;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;

namespace WaywardBeyond.Client.UI.Layers.Blueprints;

/// <summary>Controller for the modal Blueprint Library layer bound to B / Esc and the radial menu.</summary>
internal sealed class BlueprintLibraryLayer : IUILayer
{
    private const int MAX_NAME_CHARS = 64;
    private const string BUNDLED_ID_PREFIX = "bundled:";

    private readonly InteractionState _interactionState;
    private readonly IBlueprintStorage _blueprintStorage;
    private readonly IBundledBlueprintDatabase? _bundledBlueprints;
    private readonly RegionToolState _toolState;
    private readonly RegionToolService _toolService;
    private readonly NotificationService _notificationService;
    private readonly LocalizedFormatter _formatter;
    private readonly ILogger<BlueprintLibraryLayer> _logger;
    private readonly ScrollAreaState _scroll = new();
    private readonly BlueprintLibraryView _view;
    private readonly BlueprintLibraryState _state;
    private readonly ConcurrentQueue<Result<BlueprintSummary>> _completedSaves = new();

    private bool _open;
    private InteractionState.InteractionBlocker? _interactionBlocker;
    private IReadOnlyList<BlueprintSummary> _summaries = [];
    private IReadOnlyList<BlueprintSummary> _bundledSummaries = [];
    private string? _renamingId;
    private string? _confirmDeleteId;
    private string _lastSearch = string.Empty;
    private int _listRevision = -1;
    private int _renderedRevision = -2;

    /// <summary>Initializes a new instance of the <see cref="BlueprintLibraryLayer"/> class.</summary>
    public BlueprintLibraryLayer(
        in IShortcutService shortcutService,
        in IInputService inputService,
        in InteractionState interactionState,
        in IBlueprintStorage blueprintStorage,
        in RegionToolState toolState,
        in RegionToolService toolService,
        in NotificationService notificationService,
        in LocalizedFormatter formatter,
        in ILocalization localization,
        in ILogger<BlueprintLibraryLayer> logger,
        IBundledBlueprintDatabase? bundledBlueprints = null
    )
    {
        _interactionState = interactionState;
        _blueprintStorage = blueprintStorage;
        _bundledBlueprints = bundledBlueprints;
        _toolState = toolState;
        _toolService = toolService;
        _notificationService = notificationService;
        _formatter = formatter;
        _logger = logger;

        _state = new BlueprintLibraryState(
            CreateSearchTextBox(),
            CreateSaveTextBox(),
            CreateRenameTextBox(string.Empty));

        _view = new BlueprintLibraryView(localization, inputService, _scroll)
        {
            CloseRequested = Close,
            SaveRequested = OnSaveRequested,
            LoadRequested = OnLoadRequested,
            StartRenameRequested = OnStartRenameRequested,
            CommitRenameRequested = OnCommitRenameRequested,
            CancelRenameRequested = OnCancelRenameRequested,
            RequestDeleteRequested = OnRequestDeleteRequested,
            ConfirmDeleteRequested = OnConfirmDeleteRequested,
            CancelDeleteRequested = OnCancelDeleteRequested,
        };

        shortcutService.RegisterShortcut(new Shortcut
        {
            Name = "Open library",
            Category = "Blueprints",
            Modifiers = ShortcutModifiers.None,
            Key = Key.B,
            IsEnabled = () => WaywardBeyond.IsPlaying() && !_open && !_interactionState.IsInteractionBlocked(),
            Action = Open,
        });

        GameShortcuts.RegisterOrAddHandler(
            shortcutService,
            GameShortcuts.CreateBack(),
            IsVisible,
            OnEscapePressed
        );
    }

    /// <summary>Opens the Blueprint Library modal if gameplay interaction can be exclusively acquired.</summary>
    public void Open()
    {
        if (_open || !WaywardBeyond.IsPlaying())
        {
            return;
        }

        if (!_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        _open = true;
        _renamingId = null;
        _confirmDeleteId = null;
        _state.ErrorKey = string.Empty;
        _state.IsSaving = false;
        _state.CanSave = CanSaveCurrentRegion();
        _scroll.Reset();
        RefreshSummaries();
    }

    /// <summary>Closes the Blueprint Library modal and restores gameplay input.</summary>
    public void Close()
    {
        if (!_open)
        {
            return;
        }

        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _open = false;
        _renamingId = null;
        _confirmDeleteId = null;
        _state.ErrorKey = string.Empty;
        _state.IsSaving = false;
    }

    /// <inheritdoc/>
    public bool IsVisible() => _open && WaywardBeyond.IsPlaying();

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (!WaywardBeyond.IsPlaying())
        {
            Close();
            return Result.FromSuccess();
        }

        DrainCompletedSaves();
        _state.CanSave = CanSaveCurrentRegion();

        if (_state.SaveName.Submitted)
        {
            _state.SaveName.Submitted = false;
            OnSaveRequested();
        }

        if (_renamingId != null && _state.RenameText.Submitted)
        {
            _state.RenameText.Submitted = false;
            OnCommitRenameRequested(_renamingId);
        }

        string currentSearch = _state.Search.Text.ToString();
        if (!string.Equals(currentSearch, _lastSearch, StringComparison.Ordinal) || _renderedRevision != _listRevision)
        {
            _lastSearch = currentSearch;
            _renderedRevision = _listRevision;
            _state.Items = BuildFilteredItems(currentSearch);
        }

        _view.Render(ui, _state);
        _scroll.Measure(ui, "Blueprints.List");
        return Result.FromSuccess();
    }

    private void OnEscapePressed()
    {
        if (_renamingId != null)
        {
            OnCancelRenameRequested();
            return;
        }

        if (_confirmDeleteId != null)
        {
            OnCancelDeleteRequested();
            return;
        }

        Close();
    }

    private void DrainCompletedSaves()
    {
        while (_completedSaves.TryDequeue(out Result<BlueprintSummary> saved))
        {
            _state.IsSaving = false;
            if (saved.Success)
            {
                _state.SaveName = CreateSaveTextBox();
                _state.ErrorKey = string.Empty;
                RefreshSummaries();
            }
            else
            {
                _state.ErrorKey = "ui.blueprint.error.saveFailed";
            }
        }
    }

    private void OnSaveRequested()
    {
        if (_state.IsSaving)
        {
            return;
        }

        string name = _state.SaveName.Text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _state.ErrorKey = "ui.blueprint.error.nameRequired";
            return;
        }

        _state.ErrorKey = string.Empty;
        _state.IsSaving = true;
        _toolService.QueueSaveBlueprint(name, result => _completedSaves.Enqueue(result));
    }

    private void OnLoadRequested(string id)
    {
        Result<BlueprintData> loaded = id.StartsWith(BUNDLED_ID_PREFIX, StringComparison.Ordinal)
            ? _toolService.LoadBundledBlueprintToClipboard(id[BUNDLED_ID_PREFIX.Length..])
            : _toolService.LoadBlueprintToClipboard(id);
        if (!loaded.Success)
        {
            _state.ErrorKey = "ui.blueprint.error.loadFailed";
            return;
        }

        Close();
    }

    private void OnStartRenameRequested(string id)
    {
        if (id.StartsWith(BUNDLED_ID_PREFIX, StringComparison.Ordinal))
        {
            return;
        }

        string currentName = string.Empty;
        for (var i = 0; i < _summaries.Count; i++)
        {
            if (string.Equals(_summaries[i].Id, id, StringComparison.Ordinal))
            {
                currentName = _summaries[i].Name;
                break;
            }
        }

        _confirmDeleteId = null;
        _renamingId = id;
        _state.ErrorKey = string.Empty;
        _state.RenameText = CreateRenameTextBox(currentName);
        _listRevision++;
    }

    private void OnCommitRenameRequested(string id)
    {
        if (id.StartsWith(BUNDLED_ID_PREFIX, StringComparison.Ordinal))
        {
            return;
        }
        string nextName = _state.RenameText.Text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            _state.ErrorKey = "ui.blueprint.error.nameRequired";
            return;
        }

        Result<BlueprintSummary> renamed = _blueprintStorage.Rename(id, nextName);
        if (!renamed.Success)
        {
            _logger.LogWarning("Failed to rename blueprint {id}: {message}", id, renamed.Message);
            _state.ErrorKey = "ui.blueprint.error.saveFailed";
            _notificationService.Push(new Notification(_formatter.GetString("notification.blueprint.saveFailed")));
            return;
        }

        _renamingId = null;
        _state.ErrorKey = string.Empty;
        _notificationService.Push(new Notification(_formatter.GetString("notification.blueprint.renamed", renamed.Value.Name)));
        RefreshSummaries();
    }

    private void OnCancelRenameRequested()
    {
        if (_renamingId == null)
        {
            return;
        }

        _renamingId = null;
        _state.ErrorKey = string.Empty;
        _listRevision++;
    }

    private void OnRequestDeleteRequested(string id)
    {
        if (id.StartsWith(BUNDLED_ID_PREFIX, StringComparison.Ordinal))
        {
            return;
        }

        _renamingId = null;
        _confirmDeleteId = id;
        _state.ErrorKey = string.Empty;
        _listRevision++;
    }

    private void OnConfirmDeleteRequested(string id)
    {
        if (id.StartsWith(BUNDLED_ID_PREFIX, StringComparison.Ordinal))
        {
            return;
        }
        Result deleted = _blueprintStorage.Delete(id);
        if (!deleted.Success)
        {
            _logger.LogWarning("Failed to delete blueprint {id}: {message}", id, deleted.Message);
            _state.ErrorKey = "ui.blueprint.error.saveFailed";
            return;
        }

        _confirmDeleteId = null;
        _state.ErrorKey = string.Empty;
        _notificationService.Push(new Notification(_formatter.GetString("notification.blueprint.deleted")));
        RefreshSummaries();
    }

    private void OnCancelDeleteRequested()
    {
        if (_confirmDeleteId == null)
        {
            return;
        }

        _confirmDeleteId = null;
        _listRevision++;
    }

    private bool CanSaveCurrentRegion()
    {
        return _toolState.HasClipboard || _toolState.TryGetCompleteSelection(out _, out _, out _);
    }

    private void RefreshSummaries()
    {
        Result<IReadOnlyList<BlueprintSummary>> listed = _blueprintStorage.List();
        if (listed.Success && listed.Value != null)
        {
            _summaries = listed.Value;
        }
        else
        {
            _logger.LogWarning("Failed to list blueprints: {message}", listed.Message);
            _summaries = [];
        }

        _bundledSummaries = _bundledBlueprints?.ListSummaries() ?? [];
        _listRevision++;
    }

    private IReadOnlyList<BlueprintListItemState> BuildFilteredItems(string filter)
    {
        string query = filter.Trim();
        var items = new List<BlueprintListItemState>(_summaries.Count + _bundledSummaries.Count);

        for (var i = 0; i < _summaries.Count; i++)
        {
            BlueprintSummary summary = _summaries[i];
            if (query.Length > 0 && !summary.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new BlueprintListItemState(
                summary.Id,
                summary.Name,
                summary.SizeX,
                summary.SizeY,
                summary.SizeZ,
                summary.BrickCount,
                IsRenaming: string.Equals(summary.Id, _renamingId, StringComparison.Ordinal),
                ConfirmingDelete: string.Equals(summary.Id, _confirmDeleteId, StringComparison.Ordinal),
                IsReadOnly: false));
        }

        for (var i = 0; i < _bundledSummaries.Count; i++)
        {
            BlueprintSummary summary = _bundledSummaries[i];
            if (query.Length > 0 && !summary.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new BlueprintListItemState(
                BUNDLED_ID_PREFIX + summary.Id,
                summary.Name,
                summary.SizeX,
                summary.SizeY,
                summary.SizeZ,
                summary.BrickCount,
                IsRenaming: false,
                ConfirmingDelete: false,
                IsReadOnly: true));
        }

        return items;
    }

    private static TextBoxState CreateSearchTextBox()
    {
        return new TextBoxState(string.Empty, new TextBoxState.Options(MaxCharacters: MAX_NAME_CHARS));
    }

    private static TextBoxState CreateSaveTextBox()
    {
        return new TextBoxState(string.Empty, new TextBoxState.Options(MaxCharacters: MAX_NAME_CHARS));
    }

    private static TextBoxState CreateRenameTextBox(string initialValue)
    {
        var box = new TextBoxState(initialValue, new TextBoxState.Options(MaxCharacters: MAX_NAME_CHARS))
        {
            CaretIndex = initialValue.Length,
            SelectionStartIndex = initialValue.Length,
            Focused = true,
        };
        return box;
    }
}
