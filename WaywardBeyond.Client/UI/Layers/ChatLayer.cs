using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>
/// The chat overlay. One box renders in both states: a scroll viewport over the send box's reserved
/// slot. Pressing Enter opens a text box that blocks gameplay interaction (the cursor is freed so it
/// can work the box), Enter sends non-empty text and closes the box, and Escape or an empty Enter
/// closes it without sending. Closed chat sticks to the newest message and ignores the wheel. Open
/// chat starts stuck, the wheel breaks the stick, and scrolling back to the bottom re-sticks it.
/// While open, the send box keeps focus even when the user clicks elsewhere, and every message shows
/// at full alpha. When closed, each message fades on its own clock: full alpha for
/// <see cref="ChatConfig.StaleMs"/> after arrival, then a one-second fade.
/// </summary>
internal sealed class ChatLayer : IUILayer
{
    private const string TEXT_BOX_ID = "TextBox_Chat";
    private const int WIDTH = 600;
    private const int FONT_SIZE = 16;
    private const int LINE_HEIGHT = 22;
    private const int SPACING = 0;
    private const int TEXT_BOX_HEIGHT = 26;
    private const int HISTORY_VIEWPORT_HEIGHT = 200;
    private const int MAX_INPUT_CHARACTERS = 512;
    private const double FADE_DURATION = 1d;

    private readonly IInputService _inputService;
    private readonly InteractionState _interactionState;
    private readonly ChatService _chat;
    private readonly ChatConfig _config;

    private readonly FontOptions _fieldFontOptions = new()
    {
        Size = FONT_SIZE
    };
    
    private TextBoxState _textBox;
    private bool _open;
    private bool _skipNextSubmit;
    private bool _stuckToBottom = true;
    private int _contentHeight;
    private int _scrollY;
    private InteractionState.InteractionBlocker? _interactionBlocker;

    public ChatLayer(
        in IShortcutService shortcutService,
        in IInputService inputService,
        in InteractionState interactionState,
        in ChatService chat,
        in ChatConfig config,
        in ILocalization localization
    ) {
        _inputService = inputService;
        _interactionState = interactionState;
        _chat = chat;
        _config = config;

        _textBox = new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.chat"),
                MaxCharacters: MAX_INPUT_CHARACTERS,
                DisallowedCharacters: ['\t'],
                Constraints: new Constraints
                {
                    Width = new Fixed(WIDTH),
                    Height = new Fixed(TEXT_BOX_HEIGHT),
                }
            )
        );

        var openShortcut = new Shortcut
        {
            Name = "Open Chat",
            Category = "Chat",
            Key = Key.Enter,
            IsEnabled = IsOpenEnabled,
            Action = OpenChat,
        };
        shortcutService.RegisterShortcut(openShortcut);

        var closeShortcut = new Shortcut
        {
            Name = "Close Chat",
            Category = "Chat",
            Key = Key.Esc,
            IsEnabled = () => IsVisible() && _open,
            Action = CloseChat,
        };
        shortcutService.RegisterShortcut(closeShortcut);
    }

    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Playing;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (_open)
        {
            //  The send box keeps focus for the whole session; clicking elsewhere must not strand it unfocused.
            using (ui.Element(id: TEXT_BOX_ID))
            {
                ui.Focus();
            }
        }

        RenderChat(ui, _chat.Snapshot(), _open);
        return Result.FromSuccess();
    }

    private void RenderChat(UIBuilder<Material> ui, in ChatLine[] history, bool open)
    {
        //  The viewport caps at HISTORY_VIEWPORT_HEIGHT and shrinks to the content, so the newest
        //  messages hug its bottom edge in both states. The scroll extent uses the measured row
        //  heights from the last frame; line heights are font metrics, not a constant.
        int contentHeight = _contentHeight > 0 ? _contentHeight : history.Length * LINE_HEIGHT;
        int viewportHeight = Math.Min(Math.Max(contentHeight, 0), HISTORY_VIEWPORT_HEIGHT);
        int maxScroll = Math.Max(0, contentHeight - viewportHeight);

        //  Closed chat sticks to the newest message and ignores the wheel. Open chat starts stuck;
        //  the wheel breaks the stick, and scrolling back to the bottom re-sticks it.
        int scrollDelta = open ? (int)_inputService.GetMouseScroll() * LINE_HEIGHT : 0;
        if (!open)
        {
            _stuckToBottom = true;
            _scrollY = -maxScroll;
        }
        else if (_stuckToBottom)
        {
            if (scrollDelta != 0)
            {
                _stuckToBottom = false;
                _scrollY = Math.Clamp(_scrollY + scrollDelta, -maxScroll, 0);
                if (_scrollY == -maxScroll)
                {
                    _stuckToBottom = true;
                }
            }
            else
            {
                //  Staying stuck always re-pins, so new messages cannot leave the scroller behind.
                _scrollY = -maxScroll;
            }
        }
        else
        {
            _scrollY = Math.Clamp(_scrollY + scrollDelta, -maxScroll, 0);
            if (_scrollY == -maxScroll)
            {
                _stuckToBottom = true;
            }
        }

        //  Each message fades on its own clock; the overlay renders nothing once its newest message
        //  has fully faded. Messages fade in arrival order, so the faded ones are always an oldest prefix.
        long now = Stopwatch.GetTimestamp();
        double timeoutSeconds = Math.Max(0, _config.StaleMs.Get()) / 1000d;
        if (!open && (history.Length == 0 || GetMessageAlpha(now - history[^1].ReceivedAt, timeoutSeconds) <= 0f))
        {
            return;
        }

        using (ui.Element())
        {
            ui.Passthrough = !open;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Spacing = SPACING;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Left | Anchors.Bottom,
                X = new Fixed(20),
                Y = new Fixed(-40),
                Width = new Fixed(WIDTH),
            };

            if (history.Length > 0 && viewportHeight > 0)
            {
                using (ui.Element())
                {
                    ui.VerticalScroll = true;
                    ui.ScrollY = _scrollY;
                    ui.LayoutDirection = LayoutDirection.Vertical;
                    ui.Spacing = SPACING;
                    ui.ClipConstraints = new Constraints
                    {
                        Width = new Relative(1f),
                        Height = new Relative(1f),
                    };
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(WIDTH),
                        Height = new Fixed(viewportHeight),
                    };

                    if (open)
                    {
                        ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 0.1f);
                    }

                    for (var index = 0; index < history.Length; index++)
                    {
                        float alpha = open ? 1f : GetMessageAlpha(now - history[index].ReceivedAt, timeoutSeconds);
                        RenderMessage(ui, history[index].Message, alpha, !open, index);
                    }

                    //  Measure the rows this frame renders so next frame's scroll extent matches the real
                    //  row heights. Text layouts commit at Build, so this reads the previous frame's sizes.
                    int height = 0;
                    for (var index = 0; index < history.Length; index++)
                    {
                        TextLayout layout = ui.GetTextLayout($"ChatMessage_{index}");
                        height += layout.Constraints.PreferredHeight + SPACING;
                    }

                    _contentHeight = height;
                }
            }

            if (open)
            {
                ui.TextBox(TEXT_BOX_ID, ref _textBox, _fieldFontOptions, _inputService, out Widgets.Interactions interactions);
                HandleInteractions(interactions);
            }
            else
            {
                //  Reserve the send box's height so the overlay does not shift when it opens or closes.
                using (ui.Element())
                {
                    ui.Passthrough = true;
                    ui.Constraints = new Constraints
                    {
                        Width = new Fixed(WIDTH),
                        Height = new Fixed(TEXT_BOX_HEIGHT),
                    };
                }
            }
        }
    }

    private void RenderMessage(UIBuilder<Material> ui, in ChatMessage message, float alpha, bool renderBackground, int index = -1)
    {
        using (ui.Text($"#8AEBF1 {message.SenderName}: #R {message.Value}"))
        {
            ui.Passthrough = true;
            ui.FontSize = FONT_SIZE;
            ui.Color = new Vector4(1f, 1f, 1f, alpha);
            
            if (renderBackground)
            {
                ui.BackgroundColor = new Vector4(0.5f, 0.5f, 0.5f, 0.1f * alpha);
            }

            if (index >= 0)
            {
                ui.ID = $"ChatMessage_{index}";
            }
        }
    }

    private void HandleInteractions(Widgets.Interactions interactions)
    {
        if (!interactions.Has(Widgets.Interactions.Submit))
        {
            return;
        }

        if (_skipNextSubmit)
        {
            //  The Enter that opened the box arrives in the same input stream; consume the first submit after opening.
            _skipNextSubmit = false;
            return;
        }

        string text = _textBox.Text.ToString().Trim();
        ResetTextBox();

        if (string.IsNullOrEmpty(text))
        {
            CloseChat();
            return;
        }

        _chat.EnqueueSend(text);
        CloseChat();
    }

    private static float GetMessageAlpha(long ageTicks, double timeoutSeconds)
    {
        double elapsed = ageTicks / (double)Stopwatch.Frequency;
        if (elapsed < timeoutSeconds)
        {
            return 1f;
        }

        double fade = elapsed - timeoutSeconds;
        return (float)Math.Max(0d, 1d - fade / FADE_DURATION);
    }

    private bool IsOpenEnabled()
    {
        return !_open && IsVisible() && !_interactionState.IsInteractionBlocked();
    }

    private void OpenChat()
    {
        if (!_interactionState.TryBlockInteractionExclusive(out _interactionBlocker))
        {
            return;
        }

        _open = true;
        _skipNextSubmit = true;
    }

    private void CloseChat()
    {
        _interactionBlocker?.Dispose();
        _interactionBlocker = null;
        _open = false;
        ResetTextBox();
    }

    /// <summary>
    /// Clears the send box and its editing state together. Cleaning text alone would leave stale caret
    /// and selection indices in <see cref="TextBoxState"/>, which crash the text box on the next edit.
    /// </summary>
    private void ResetTextBox()
    {
        _textBox.Text.Clear();
        _textBox.CaretIndex = 0;
        _textBox.SelectionStartIndex = 0;
        _textBox.ShiftModifier = false;
        _textBox.ControlModifier = false;
    }
}