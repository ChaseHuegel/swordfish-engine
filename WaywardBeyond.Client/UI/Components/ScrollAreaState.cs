using System;
using Reef;
using Reef.UI;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Scroll state for one markup scroll area.</summary>
internal sealed class ScrollAreaState
{
    private readonly int _step;
    private readonly ScrollBarState _bar;
    private int _viewportHeight;
    private int _contentHeight;
    private int _maxScroll;

    /// <summary>Initializes the scroll state.</summary>
    public ScrollAreaState(int step = 64)
    {
        _step = step;
        _bar = new ScrollBarState(onProgress: SetProgress);
    }

    /// <summary>Gets the current scroll offset.</summary>
    public int Offset { get; private set; }

    /// <summary>Gets the negative offset used by Reef.</summary>
    public int ReefOffset => -Offset;

    /// <summary>Gets the scrollbar geometry.</summary>
    public ScrollBarState Bar => _bar;

    /// <summary>Updates the metrics from the bounds of one scroll area.</summary>
    public void Measure<T>(UIBuilder<T> ui, string idPrefix)
    {
        ElementBounds viewport = ui.GetElementBounds(idPrefix);
        ElementBounds content = ui.GetElementBounds(idPrefix + "Content");
        if (viewport.Rect.Size.Y > 0 && content.Rect.Size.Y > 0)
        {
            SetMetrics(viewport.Rect.Size.Y, content.Rect.Size.Y);
        }
    }

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
        SetOffset(0);
    }

    /// <summary>Applies one wheel delta.</summary>
    public void OnScroll(float delta)
    {
        SetOffset(Offset - (int)MathF.Round(delta * _step));
    }

    private void SetProgress(float progress)
    {
        SetOffset((int)MathF.Round(progress * _maxScroll));
    }

    private void SetOffset(int offset)
    {
        Offset = Math.Clamp(offset, 0, _maxScroll);
        UpdateBar();
    }

    private void UpdateBar()
    {
        if (_contentHeight <= 0 || _viewportHeight <= 0)
        {
            _bar.SetGeometry(1f, 0f);
            return;
        }

        float ratio = Math.Clamp((float)_viewportHeight / _contentHeight, 0f, 1f);
        float progress = _maxScroll > 0 ? (float)Offset / _maxScroll : 0f;
        _bar.SetGeometry(ratio, progress * (1f - ratio));
    }
}
