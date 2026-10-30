using System;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Scroll state for one markup scroll area.</summary>
internal sealed class ScrollAreaState(int step = 64)
{
    private int _viewportHeight;
    private int _contentHeight;
    private int _maxScroll;

    /// <summary>Gets the current scroll offset.</summary>
    public int Offset { get; private set; }

    /// <summary>Gets the negative offset used by Reef.</summary>
    public int ReefOffset => -Offset;

    /// <summary>Gets the scrollbar geometry.</summary>
    public ScrollBarState Bar { get; private set; } = new(1f, 0f);

    /// <summary>Updates the viewport and content metrics.</summary>
    public void SetMetrics(int viewportHeight, int contentHeight)
    {
        _viewportHeight = viewportHeight;
        _contentHeight = contentHeight;
        _maxScroll = Math.Max(0, contentHeight - viewportHeight);
        Offset = Math.Clamp(Offset, 0, _maxScroll);
        UpdateBar();
    }

    /// <summary>Scrolls back to the top.</summary>
    public void Reset()
    {
        Offset = 0;
        UpdateBar();
    }

    /// <summary>Applies one wheel delta.</summary>
    public void OnScroll(float delta)
    {
        Offset = Math.Clamp(
            Offset - (int)MathF.Round(delta * step),
            0,
            _maxScroll
        );
        UpdateBar();
    }

    private void UpdateBar()
    {
        if (_contentHeight <= 0 || _viewportHeight <= 0)
        {
            Bar = new ScrollBarState(1f, 0f);
            return;
        }

        float ratio = Math.Clamp((float)_viewportHeight / _contentHeight, 0f, 1f);
        float progress = _maxScroll > 0 ? (float)Offset / _maxScroll : 0f;
        Bar = new ScrollBarState(ratio, progress * (1f - ratio));
    }
}
