using System;
using System.Diagnostics;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>
/// The chat overlay. One fixed-geometry box renders in both states: a 200px scroll viewport over the
/// send box's reserved slot. Pressing Enter opens a text box that blocks gameplay interaction (the
/// cursor is freed so it can work the box), Enter sends non-empty text and closes the box, and Escape
/// or an empty Enter closes it without sending. Closed chat sticks to the newest message and ignores
/// the wheel. Open chat starts stuck, the wheel breaks the stick, and scrolling back to the bottom
/// re-sticks it. While open, the send box keeps focus even when the user clicks elsewhere, and every
/// message shows at full alpha. When closed, each message fades on its own clock: full alpha for
/// <see cref="ChatConfig.StaleMs"/> after arrival, then a one-second fade.
/// </summary>
internal sealed class ChatLayer : IUILayer
{
    private const string TEXT_BOX_ID = "TextBox_Chat";
    private const int WIDTH = 600;
    private const int HEADER_HEIGHT = 28;
    private const int HISTORY_VIEWPORT_HEIGHT = 200;
    private const int VIEWPORT_X = 6;
    private const int VIEWPORT_Y = HEADER_HEIGHT + 4;
    private const int VIEWPORT_WIDTH = WIDTH - 20;
    private const int SCROLLBAR_X = WIDTH - 10;
    private const int SCROLLBAR_WIDTH = 4;
    private const int INPUT_BAR_X = 6;
    private const int INPUT_BAR_Y = VIEWPORT_Y + HISTORY_VIEWPORT_HEIGHT + 4;
    private const int INPUT_BAR_WIDTH = WIDTH - 12;
    private const int INPUT_BAR_HEIGHT = 28;
    private const int PANEL_HEIGHT = INPUT_BAR_Y + INPUT_BAR_HEIGHT + 6;
    private const int PROMPT_WIDTH = 22;
    private const int SEND_HINT_WIDTH = 68;
    private const int TEXT_BOX_WIDTH = INPUT_BAR_WIDTH - PROMPT_WIDTH - SEND_HINT_WIDTH - 8;
    private const int TEXT_BOX_HEIGHT = 18;
    private const int LINE_HEIGHT = 22;
    private const int DEFAULT_TEXT_HEIGHT = 16;
    private const int MESSAGE_SPACING = 3;
    private const int MESSAGE_BAR_WIDTH = 3;
    private const int MESSAGE_TEXT_LEFT_PADDING = 10;
    private const int MESSAGE_PADDING_X = 8;
    private const int MESSAGE_PADDING_Y = 3;
    private const float CLOSED_MESSAGE_BG_ALPHA = 0.78f;
    private const int MAX_INPUT_CHARACTERS = 512;
    private const double FADE_DURATION = 1d;

    private readonly IInputService _inputService;
    private readonly InteractionState _interactionState;
    private readonly ChatService _chat;
    private readonly ChatConfig _config;
    private readonly string _headerLabel;
    private readonly string _closeHintLabel;
    private readonly string _sendHintLabel;

    private readonly FontOptions _fieldFontOptions = new()
    {
        Size = UiMetrics.SMALL_FONT,
    };

    private TextBoxState _textBox;
    private bool _open;
    private bool _skipNextSubmit;
    private bool _stuckToBottom = true;
    private int _scrollY;
    private InteractionState.InteractionBlocker? _interactionBlocker;

    /// <summary>Initializes a new instance of the <see cref="ChatLayer"/> class.</summary>
    public ChatLayer(
        in IShortcutService shortcutService,
        in IInputService inputService,
        in InteractionState interactionState,
        in ChatService chat,
        in ChatConfig config,
        in ILocalization localization
    )
    {
        _inputService = inputService;
        _interactionState = interactionState;
        _chat = chat;
        _config = config;
        _headerLabel = localization.GetString("ui.chat.header") ?? "SUBSPACE COMMS // LOCAL";
        _closeHintLabel = localization.GetString("ui.chat.closeHint") ?? "[ESC] CLOSE";
        _sendHintLabel = localization.GetString("ui.chat.sendHint") ?? "[ENTER]";

        _textBox = new TextBoxState(
            initialValue: string.Empty,
            new TextBoxState.Options(
                Placeholder: localization.GetString("ui.field.chat"),
                MaxCharacters: MAX_INPUT_CHARACTERS,
                DisallowedCharacters: ['\t'],
                Constraints: new Constraints
                {
                    X = new Fixed(PROMPT_WIDTH),
                    Y = new Fixed((INPUT_BAR_HEIGHT - TEXT_BOX_HEIGHT) / 2),
                    Width = new Fixed(TEXT_BOX_WIDTH),
                    Height = new Fixed(TEXT_BOX_HEIGHT),
                },
                Chrome: false
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

    /// <inheritdoc/>
    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Playing;
    }

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        if (_open)
        {
            //  The send box keeps focus for the whole session; clicking elsewhere must not strand it unfocused.
            using (ui.Element(id: TEXT_BOX_ID))
            {
                ui.Passthrough = true;
                ui.Focus();
            }
        }

        RenderChat(ui, _chat.Snapshot(), _open);
        return Result.FromSuccess();
    }

    private void RenderChat(UIBuilder<Material> ui, in ChatLine[] history, bool open)
    {
        //  Each message fades on its own clock; the closed overlay renders nothing once its newest
        //  message has fully faded. Messages fade in arrival order, so faded ones are an oldest prefix.
        long now = Stopwatch.GetTimestamp();
        double timeoutSeconds = Math.Max(0, _config.StaleMs.Get()) / 1000d;
        if (!open && (history.Length == 0 || GetMessageAlpha(now - history[^1].ReceivedAt, timeoutSeconds) <= 0f))
        {
            return;
        }

        int contentHeight = MeasureContentHeight(ui, history.Length);
        int maxScroll = Math.Max(0, contentHeight - HISTORY_VIEWPORT_HEIGHT);
        int viewportOffsetY = UpdateScroll(open, contentHeight, maxScroll);

        using (ui.Element())
        {
            ui.Passthrough = !open;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Left | Anchors.Bottom,
                X = new Fixed(20),
                Y = new Fixed(-40),
                Width = new Fixed(WIDTH),
                Height = new Fixed(PANEL_HEIGHT),
            };

            if (open)
            {
                RenderOpenPanelChrome(ui);
            }

            if (history.Length > 0)
            {
                RenderHistoryViewport(ui, history, open, now, timeoutSeconds, viewportOffsetY);
            }

            if (open)
            {
                RenderScrollbar(ui, contentHeight, maxScroll);
                RenderInputBar(ui);
            }
        }
    }

    /// <summary>Updates the scroll state and returns the content offset for this frame.</summary>
    private int UpdateScroll(bool open, int contentHeight, int maxScroll)
    {
        //  Closed chat sticks to the newest message and ignores the wheel. Open chat starts stuck;
        //  the wheel breaks the stick, and scrolling back to the bottom re-sticks it.
        int scrollDelta = 0;
        if (open)
        {
            scrollDelta = (int)_inputService.GetMouseScroll() * LINE_HEIGHT;
        }

        if (!open)
        {
            _stuckToBottom = true;
            _scrollY = -maxScroll;
        }
        else if (_stuckToBottom)
        {
            if (scrollDelta == 0)
            {
                //  Staying stuck always re-pins, so new messages cannot leave the scroller behind.
                _scrollY = -maxScroll;
            }
            else
            {
                _stuckToBottom = false;
                _scrollY = Math.Clamp(_scrollY + scrollDelta, -maxScroll, 0);
                if (_scrollY == -maxScroll)
                {
                    _stuckToBottom = true;
                }
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

        if (contentHeight < HISTORY_VIEWPORT_HEIGHT)
        {
            //  When there are only a few messages, push them to the bottom of the fixed viewport.
            return HISTORY_VIEWPORT_HEIGHT - contentHeight;
        }

        return _scrollY;
    }

    private void RenderOpenPanelChrome(UIBuilder<Material> ui)
    {
        var border = new BorderStyle
        {
            Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
            Color = UiTheme.Border,
        };
        var caps = new CornerCapStyle
        {
            Length = UiMetrics.PANEL_CAP_LENGTH,
            Thickness = UiMetrics.PANEL_CAP_THICKNESS,
            Color = UiTheme.Accent,
        };

        using (ui.Panel(Widgets.Rect(0, 0, WIDTH, PANEL_HEIGHT), UiTheme.PanelBackground, border, caps, innerBorder: true))
        {
            int headerInnerWidth = WIDTH - 6;
            int headerInnerHeight = HEADER_HEIGHT - 3;

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.PanelInner;
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(3, 3, headerInnerWidth, headerInnerHeight));

                //  Left cyan accent pip
                using (ui.Element())
                {
                    ui.Color = UiTheme.Accent;
                    ui.Constraints = Widgets.RectConstraints(Widgets.Rect(8, 8, 4, 9));
                }

                ui.Label(
                    _headerLabel,
                    Widgets.Rect(18, 0, 360, headerInnerHeight),
                    UiMetrics.TINY_FONT,
                    UiTheme.TextMuted,
                    Anchors.Left | Anchors.Center
                );

                ui.Label(
                    _closeHintLabel,
                    Widgets.Rect(headerInnerWidth - 160, 0, 152, headerInnerHeight),
                    UiMetrics.TINY_FONT,
                    UiTheme.TextDim,
                    Anchors.Right | Anchors.Center
                );
            }

            ui.Divider(Widgets.Rect(3, HEADER_HEIGHT, headerInnerWidth, 1), UiTheme.BorderInner);
        }
    }

    private void RenderHistoryViewport(
        UIBuilder<Material> ui,
        in ChatLine[] history,
        bool open,
        long now,
        double timeoutSeconds,
        int viewportOffsetY
    )
    {
        using (ui.Element())
        {
            ui.Passthrough = !open;
            ui.VerticalScroll = true;
            ui.ScrollY = viewportOffsetY;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Spacing = MESSAGE_SPACING;
            ui.Constraints = Widgets.RectConstraints(Widgets.Rect(VIEWPORT_X, VIEWPORT_Y, VIEWPORT_WIDTH, HISTORY_VIEWPORT_HEIGHT));
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(VIEWPORT_WIDTH),
                Height = new Fixed(HISTORY_VIEWPORT_HEIGHT),
            };

            for (var index = 0; index < history.Length; index++)
            {
                float alpha;
                if (open)
                {
                    alpha = 1f;
                }
                else
                {
                    alpha = GetMessageAlpha(now - history[index].ReceivedAt, timeoutSeconds);
                }

                int rowHeight = GetMessageRowHeight(ui, index);
                RenderMessage(ui, history[index].Message, alpha, open, index, rowHeight);
            }
        }
    }

    private void RenderScrollbar(UIBuilder<Material> ui, int contentHeight, int maxScroll)
    {
        IntRect scrollbarRect = Widgets.Rect(SCROLLBAR_X, VIEWPORT_Y, SCROLLBAR_WIDTH, HISTORY_VIEWPORT_HEIGHT);
        if (maxScroll > 0)
        {
            ui.Scrollbar(scrollbarRect, contentHeight, HISTORY_VIEWPORT_HEIGHT, -_scrollY);
            return;
        }

        using (ui.Element())
        {
            ui.Color = UiTheme.Track;
            ui.Constraints = Widgets.RectConstraints(scrollbarRect);
            ui.ClipConstraints = Widgets.RectConstraints(scrollbarRect);
        }
    }

    private void RenderInputBar(UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.AccentDark;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = UiTheme.Accent,
            };
            ui.Constraints = Widgets.RectConstraints(Widgets.Rect(INPUT_BAR_X, INPUT_BAR_Y, INPUT_BAR_WIDTH, INPUT_BAR_HEIGHT));
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(INPUT_BAR_WIDTH),
                Height = new Fixed(INPUT_BAR_HEIGHT),
            };

            ui.Label(
                ">",
                Widgets.Rect(8, 0, 14, INPUT_BAR_HEIGHT),
                UiMetrics.SMALL_FONT,
                UiTheme.Accent,
                Anchors.Left | Anchors.Center
            );

            ui.TextBox(
                TEXT_BOX_ID,
                ref _textBox,
                _fieldFontOptions,
                _inputService,
                out Widgets.Interactions interactions
            );
            HandleInteractions(interactions);

            ui.Label(
                _sendHintLabel,
                Widgets.Rect(INPUT_BAR_WIDTH - SEND_HINT_WIDTH - 8, 0, SEND_HINT_WIDTH, INPUT_BAR_HEIGHT),
                UiMetrics.TINY_FONT,
                UiTheme.AccentBright,
                Anchors.Right | Anchors.Center
            );
        }
    }

    private static void RenderMessage(
        UIBuilder<Material> ui,
        in ChatMessage message,
        float alpha,
        bool open,
        int index,
        int rowHeight
    )
    {
        var fade = new Vector4(1f, 1f, 1f, alpha);

        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Width = new Fixed(VIEWPORT_WIDTH),
                Height = new Fixed(rowHeight),
            };

            if (!open && alpha > 0f)
            {
                ui.Color = UiTheme.PanelInner * new Vector4(1f, 1f, 1f, CLOSED_MESSAGE_BG_ALPHA * alpha);

                //  Vertical cyan bar anchor on the left edge of inactive chat messages
                using (ui.Element())
                {
                    ui.Passthrough = true;
                    ui.Color = UiTheme.Accent * fade;
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(0),
                        Y = new Fixed(0),
                        Width = new Fixed(MESSAGE_BAR_WIDTH),
                        Height = new Fixed(rowHeight),
                    };
                }
            }

            using (ui.Element())
            {
                ui.Passthrough = true;
                ui.Padding = new Padding(
                    left: MESSAGE_TEXT_LEFT_PADDING,
                    top: MESSAGE_PADDING_Y,
                    right: MESSAGE_PADDING_X,
                    bottom: MESSAGE_PADDING_Y
                );
                ui.Constraints = new Constraints
                {
                    Width = new Fixed(VIEWPORT_WIDTH),
                    Height = new Fixed(rowHeight),
                };

                using (ui.Text($"#A5F3FC [{message.SenderName}] #R {message.Value}"))
                {
                    ui.ID = $"ChatMessage_{index}";
                    ui.Passthrough = true;
                    ui.FontSize = UiMetrics.SMALL_FONT;
                    ui.Color = UiTheme.Text * fade;
                }
            }
        }
    }

    private static int MeasureContentHeight(UIBuilder<Material> ui, int messageCount)
    {
        if (messageCount <= 0)
        {
            return 0;
        }

        int totalHeight = 0;
        for (var index = 0; index < messageCount; index++)
        {
            totalHeight += GetMessageRowHeight(ui, index);
        }

        totalHeight += (messageCount - 1) * MESSAGE_SPACING;
        return totalHeight;
    }

    private static int GetMessageRowHeight(UIBuilder<Material> ui, int index)
    {
        TextLayout layout = ui.GetTextLayout($"ChatMessage_{index}");
        int textHeight;
        if (layout.Constraints.PreferredHeight > 0)
        {
            textHeight = layout.Constraints.PreferredHeight;
        }
        else
        {
            textHeight = DEFAULT_TEXT_HEIGHT;
        }

        return textHeight + MESSAGE_PADDING_Y * 2;
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
