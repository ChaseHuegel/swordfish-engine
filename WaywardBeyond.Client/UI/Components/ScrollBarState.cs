using System;
using Reef.Constraints;
using Reef.UI;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Thumb geometry and drag state for a scrollbar.</summary>
internal sealed class ScrollBarState(float thumbRatio = 1f, float thumbOffset = 0f, Action<float>? onProgress = null)
{
    private readonly Action<float>? _onProgress = onProgress;
    private float _thumbRatio = thumbRatio;
    private float _thumbOffset = thumbOffset;

    // Reef keeps the active pointer on the pressed element until release, so
    // the grab offset describes the whole drag without a separate drag flag.
    private float _grabOffset;

    /// <summary>Gets the thumb ratio.</summary>
    public float ThumbRatio => _thumbRatio;

    /// <summary>Gets the normalized thumb offset.</summary>
    public float ThumbOffset => _thumbOffset;

    /// <summary>Gets whether the thumb is smaller than the track.</summary>
    public bool HasThumb => _thumbRatio < 1f;

    /// <summary>Gets the thumb height constraint.</summary>
    public IConstraint ThumbHeight => new Relative(Math.Clamp(_thumbRatio, 0f, 1f));

    /// <summary>Gets the thumb offset constraint.</summary>
    public IConstraint ThumbY => new Relative(Math.Clamp(_thumbOffset, 0f, 1f));

    /// <summary>Updates the thumb geometry.</summary>
    public void SetGeometry(float thumbRatio, float thumbOffset)
    {
        _thumbRatio = thumbRatio;
        _thumbOffset = thumbOffset;
    }

    /// <summary>Starts a thumb drag or jumps toward a track click.</summary>
    public void OnPress(int localY, int trackHeight)
    {
        if (!HasThumb || trackHeight <= 0)
        {
            return;
        }

        float thumbHeight = _thumbRatio * trackHeight;
        float thumbTop = _thumbOffset * trackHeight;
        if (localY >= thumbTop && localY < thumbTop + thumbHeight)
        {
            _grabOffset = localY - thumbTop;
            return;
        }

        _grabOffset = thumbHeight / 2f;
        MoveThumb(localY - _grabOffset, trackHeight);
    }

    /// <summary>Continues a thumb drag while the mouse button stays held.</summary>
    public void OnHeld(int localY, int trackHeight)
    {
        if (!HasThumb || trackHeight <= 0)
        {
            return;
        }

        MoveThumb(localY - _grabOffset, trackHeight);
    }

    private void MoveThumb(float top, int trackHeight)
    {
        float travel = trackHeight * (1f - _thumbRatio);
        if (travel <= 0f)
        {
            return;
        }

        float progress = Math.Clamp(top / travel, 0f, 1f);
        _onProgress?.Invoke(progress);
    }
}
