using System;
using System.Diagnostics;
using Reef;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;

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
    private const double FADE_DURATION = 1d;

    private readonly IInputService _inputService;
    private readonly InteractionState _interactionState;
    private readonly ChatService _chat;
    private readonly ChatConfig _config;
    private readonly ChatView _view;

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
        _view = new ChatView(localization)
        {
            Input = inputService,
        };

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
        //  Each message fades on its own clock; the closed overlay renders nothing once its newest
        //  message has fully faded. Messages fade in arrival order, so faded ones are an oldest prefix.
        long now = Stopwatch.GetTimestamp();
        double timeoutSeconds = Math.Max(0, _config.StaleMs.Get()) / 1000d;
        ChatLine[] history = _chat.Snapshot();
        if (!_open && (history.Length == 0 || GetMessageAlpha(now - history[^1].ReceivedAt, timeoutSeconds) <= 0f))
        {
            return Result.FromSuccess();
        }

        int contentHeight = MeasureContentHeight(ui, history.Length);
        int maxScroll = Math.Max(0, contentHeight - ChatView.ViewportHeight);
        int viewportOffsetY = UpdateScroll(contentHeight, maxScroll);

        var rows = new ChatMessageRowState[history.Length];
        for (var index = 0; index < history.Length; index++)
        {
            float alpha = _open
                ? 1f
                : GetMessageAlpha(now - history[index].ReceivedAt, timeoutSeconds);

            rows[index] = new ChatMessageRowState(
                history[index].Message.SenderName,
                history[index].Message.Value,
                GetMessageRowHeight(ui, index),
                alpha,
                !_open && alpha > 0f
            );
        }

        GetScrollbarMetrics(contentHeight, maxScroll, out bool showThumb, out float thumbRatio, out float thumbOffset);
        _view.Render(
            ui,
            new ChatViewState(_open, rows, showThumb, thumbRatio, thumbOffset, viewportOffsetY)
        );

        if (_open && _view.TakeSubmit())
        {
            HandleSubmit();
        }

        return Result.FromSuccess();
    }

    /// <summary>Updates the scroll state and returns the content offset for this frame.</summary>
    private int UpdateScroll(int contentHeight, int maxScroll)
    {
        //  Closed chat sticks to the newest message and ignores the wheel. Open chat starts stuck;
        //  the wheel breaks the stick, and scrolling back to the bottom re-sticks it.
        int scrollDelta = 0;
        if (_open)
        {
            scrollDelta = (int)_inputService.GetMouseScroll() * ChatView.LineHeight;
        }

        if (!_open)
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

        if (contentHeight < ChatView.ViewportHeight)
        {
            //  When there are only a few messages, push them to the bottom of the fixed viewport.
            return ChatView.ViewportHeight - contentHeight;
        }

        return _scrollY;
    }

    private void HandleSubmit()
    {
        if (_skipNextSubmit)
        {
            //  The Enter that opened the box arrives in the same input stream; consume the first submit after opening.
            _skipNextSubmit = false;
            return;
        }

        string text = _view.ChatBox.Text.ToString().Trim();
        _view.ResetInput();

        if (string.IsNullOrEmpty(text))
        {
            CloseChat();
            return;
        }

        _chat.EnqueueSend(text);
        CloseChat();
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

        totalHeight += (messageCount - 1) * ChatView.MessageSpacing;
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
            textHeight = ChatView.DefaultTextHeight;
        }

        return textHeight + ChatView.MessagePaddingY * 2;
    }

    private void GetScrollbarMetrics(
        int contentHeight,
        int maxScroll,
        out bool showThumb,
        out float thumbRatio,
        out float thumbOffset
    )
    {
        showThumb = false;
        thumbRatio = 0f;
        thumbOffset = 0f;
        if (maxScroll <= 0)
        {
            return;
        }

        int thumbHeight = Math.Clamp(
            ChatView.ViewportHeight * ChatView.ViewportHeight / contentHeight,
            ChatView.ScrollbarThumbMinHeight,
            ChatView.ViewportHeight
        );
        int scrollY = Math.Clamp(-_scrollY, 0, maxScroll);
        int thumbY = (ChatView.ViewportHeight - thumbHeight) * scrollY / maxScroll;

        showThumb = true;
        thumbRatio = thumbHeight / (float)ChatView.ViewportHeight;
        thumbOffset = thumbY / (float)ChatView.ViewportHeight;
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
        _view.ResetInput();
    }
}
