using System;
using System.Collections.Generic;
using System.Numerics;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for one chat message row.</summary>
internal readonly record struct ChatMessageRowState(
    string Sender,
    string Value,
    int Height,
    float Alpha,
    bool ShowClosedChrome
);

/// <summary>Display data for the chat overlay.</summary>
internal readonly record struct ChatViewState(
    bool Open,
    IReadOnlyList<ChatMessageRowState> Messages,
    bool ShowThumb,
    float ThumbRatio,
    float ThumbOffset,
    int ViewportOffsetY
)
{
    /// <summary>Gets whether any messages are present.</summary>
    public bool HasHistory => Messages.Count > 0;

    /// <summary>Gets the scrollbar thumb height constraint.</summary>
    public IConstraint ThumbHeight => new Relative(Math.Clamp(ThumbRatio, 0f, 1f));

    /// <summary>Gets the scrollbar thumb offset constraint.</summary>
    public IConstraint ThumbOffsetY => new Relative(Math.Clamp(ThumbOffset, 0f, 1f));
}

/// <summary>Draws the chat history and send bar.</summary>
internal partial class ChatView : UiViewBase
{
    /// <summary>The history viewport height.</summary>
    public const int ViewportHeight = 200;

    /// <summary>The gap between message rows.</summary>
    public const int MessageSpacing = 3;

    /// <summary>The fallback text height before a layout exists.</summary>
    public const int DefaultTextHeight = 16;

    /// <summary>The vertical padding inside a message row.</summary>
    public const int MessagePaddingY = 3;

    /// <summary>The wheel step in pixels.</summary>
    public const int LineHeight = 22;

    /// <summary>The minimum scrollbar thumb height.</summary>
    public const int ScrollbarThumbMinHeight = 24;

    private const int MAX_INPUT_CHARACTERS = 512;
    private const float CLOSED_MESSAGE_BACKGROUND_ALPHA = 0.78f;

    /// <summary>Initializes the chat view.</summary>
    public ChatView(ILocalization localization) : base(localization)
    {
        ChatBox = new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                MaxCharacters: MAX_INPUT_CHARACTERS,
                DisallowedCharacters: ['\t']
            )
        );
    }

    /// <summary>The editable send box state.</summary>
    public TextBoxState ChatBox;

    /// <summary>Gets or sets the input service the send box reads.</summary>
    public IInputService Input { get; set; } = null!;

    /// <summary>Returns true once when the send box submitted this frame.</summary>
    public bool TakeSubmit()
    {
        if (!ChatBox.Submitted)
        {
            return false;
        }

        ChatBox.Submitted = false;
        return true;
    }

    /// <summary>Clears the send box and its editing state together.</summary>
    public void ResetInput()
    {
        ChatBox.Text.Clear();
        ChatBox.CaretIndex = 0;
        ChatBox.SelectionStartIndex = 0;
        ChatBox.ShiftModifier = false;
        ChatBox.ControlModifier = false;
        ChatBox.Submitted = false;
    }

    /// <summary>Formats the sender run followed by the message rich text.</summary>
    protected static string MessageText(string sender, string value)
    {
        return $"{UiTheme.AccentBrightRichText} {sender}: #R {value}";
    }

    /// <summary>Returns the closed message background, or transparent when open.</summary>
    protected static Vector4 MessageBackground(float alpha, bool showClosedChrome)
    {
        return showClosedChrome
            ? UiTheme.PanelInner * new Vector4(1f, 1f, 1f, CLOSED_MESSAGE_BACKGROUND_ALPHA * alpha)
            : Vector4.Zero;
    }

    /// <summary>Returns the faded accent bar color.</summary>
    protected static Vector4 AccentColor(float alpha)
    {
        return UiTheme.Accent * new Vector4(1f, 1f, 1f, alpha);
    }

    /// <summary>Returns the faded message text color.</summary>
    protected static Vector4 TextColor(float alpha)
    {
        return UiTheme.Text * new Vector4(1f, 1f, 1f, alpha);
    }
}
