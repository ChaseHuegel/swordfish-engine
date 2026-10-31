using System;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards multiplayer interactions and formats server values.</summary>
internal partial class MultiplayerView : UiViewBase
{
    private readonly ILocalization _localization;

    /// <summary>Initializes the multiplayer view.</summary>
    public MultiplayerView(ILocalization localization, ScrollAreaState scroll) : base(localization)
    {
        _localization = localization;
        Scroll = scroll;
        HostState = new TextBoxState(
            initialValue: string.Empty,
            options: new TextBoxState.Options(
                Placeholder: L("ui.field.server.host"),
                MaxCharacters: 253
            )
        );
        PortState = new TextBoxState(
            initialValue: string.Empty,
            options: new TextBoxState.Options(
                Placeholder: L("ui.field.server.port"),
                MaxCharacters: 5,
                DisallowedCharacters: [' ', '\t', '\n', '\r']
            )
        );
    }

    /// <summary>Gets the server list scroll state.</summary>
    public ScrollAreaState Scroll { get; }

    /// <summary>The editable host state.</summary>
    public TextBoxState HostState;

    /// <summary>The editable port state.</summary>
    public TextBoxState PortState;

    /// <summary>Gets or sets the input service the editable fields read.</summary>
    public IInputService Input { get; set; } = null!;

    /// <summary>Raised when a tab is clicked.</summary>
    public Action<bool>? TabRequested { get; set; }

    /// <summary>Raised when the LAN scan is restarted.</summary>
    public Action? ScanRequested { get; set; }

    /// <summary>Raised when a server row is selected.</summary>
    public Action<int>? RowSelected { get; set; }

    /// <summary>Raised when a server row action is clicked.</summary>
    public Action<int>? RowActionRequested { get; set; }

    /// <summary>Raised when the add favorite button is clicked.</summary>
    public Action? AddFavoriteRequested { get; set; }

    /// <summary>Raised when disconnect is clicked.</summary>
    public Action? DisconnectRequested { get; set; }

    /// <summary>Raised when the saved level list is requested.</summary>
    public Action? ViewSavesRequested { get; set; }

    /// <summary>Raised when back is clicked.</summary>
    public Action? BackRequested { get; set; }

    /// <summary>Raised when join is clicked.</summary>
    public Action? JoinRequested { get; set; }

    /// <summary>Raised when connect is clicked.</summary>
    public Action? ConnectRequested { get; set; }

    partial void OnSelectFavorites() => TabRequested?.Invoke(true);

    partial void OnSelectLan() => TabRequested?.Invoke(false);

    partial void OnScan() => ScanRequested?.Invoke();

    partial void OnSelectRow(int index) => RowSelected?.Invoke(index);

    partial void OnRowAction(int index) => RowActionRequested?.Invoke(index);

    partial void OnAddFavorite() => AddFavoriteRequested?.Invoke();

    partial void OnDisconnect() => DisconnectRequested?.Invoke();

    partial void OnViewSaves() => ViewSavesRequested?.Invoke();

    partial void OnBack() => BackRequested?.Invoke();

    partial void OnJoin() => JoinRequested?.Invoke();

    partial void OnConnect() => ConnectRequested?.Invoke();

    /// <summary>Formats the empty state message of the active tab.</summary>
    public string EmptyText(bool favorites, bool scanning, string? discoverMessage)
    {
        if (favorites)
        {
            return L("ui.label.none");
        }

        return scanning
            ? L("ui.message.server.discover.scanning")
            : discoverMessage ?? L("ui.message.server.discover.empty");
    }

    /// <summary>Gets a localized label, or the key when the string is missing.</summary>
    public string Label(string key)
    {
        return _localization.GetRequired(key);
    }
}
