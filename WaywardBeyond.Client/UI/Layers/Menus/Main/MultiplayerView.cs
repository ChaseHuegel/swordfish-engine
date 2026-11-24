using System;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards multiplayer interactions and formats server values.</summary>
internal partial class MultiplayerView : UiViewBase
{
    /// <summary>Initializes the multiplayer view.</summary>
    public MultiplayerView(ILocalization localization, ScrollAreaState scroll) : base(localization)
    {
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

    /// <summary>Formats a server address.</summary>
    public static string AddressText(string host, int port)
    {
        return $"{host}:{port}";
    }

    /// <summary>Returns the saved name of the connected server, or its address.</summary>
    public static string BannerServerText(string name, string host, int port)
    {
        return name.Length > 0 ? name : AddressText(host, port);
    }

    /// <summary>Formats the empty state message of the active tab.</summary>
    public string EmptyText(bool favorites, bool scanning, string discoverKey)
    {
        if (favorites)
        {
            return L("ui.label.none");
        }

        if (scanning)
        {
            return L("ui.message.server.discover.scanning");
        }

        return discoverKey.Length > 0 ? L(discoverKey) : L("ui.message.server.discover.empty");
    }

    /// <summary>Returns the ping icon glyph for a measured latency.</summary>
    public static IconGlyph PingGlyph(int? pingMs)
    {
        return pingMs.HasValue && pingMs.Value >= 250 ? IconGlyph.XMark : IconGlyph.Signal;
    }

    /// <summary>Returns the ping icon tone for a measured latency.</summary>
    public static IconTone PingTone(int? pingMs)
    {
        if (!pingMs.HasValue)
        {
            return IconTone.Dim;
        }

        int value = pingMs.Value;
        if (value < 60)
        {
            return IconTone.Success;
        }

        if (value < 100)
        {
            return IconTone.Warning;
        }

        if (value < 150)
        {
            return IconTone.Caution;
        }

        return IconTone.Danger;
    }

    /// <summary>Formats a ping reading for the server list.</summary>
    public static string PingText(int? pingMs)
    {
        if (!pingMs.HasValue)
        {
            return "-";
        }

        return pingMs.Value < 1 ? "<1 ms" : $"{pingMs.Value} ms";
    }

    /// <summary>True when the ping is below 60 ms.</summary>
    public static bool IsPingGood(int? pingMs) => pingMs.HasValue && pingMs.Value < 60;

    /// <summary>True when the ping is between 60 and 99 ms.</summary>
    public static bool IsPingFair(int? pingMs) => pingMs.HasValue && pingMs.Value >= 60 && pingMs.Value < 100;

    /// <summary>True when the ping is between 100 and 149 ms.</summary>
    public static bool IsPingPoor(int? pingMs) => pingMs.HasValue && pingMs.Value >= 100 && pingMs.Value < 150;

    /// <summary>True when the ping is 150 ms or more.</summary>
    public static bool IsPingBad(int? pingMs) => pingMs.HasValue && pingMs.Value >= 150;
}
