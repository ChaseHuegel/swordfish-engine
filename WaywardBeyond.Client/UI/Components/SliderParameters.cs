using System;
using Reef.Constraints;

using System;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared slider control.</summary>
internal sealed record SliderParameters(
    string Id = "",
    IConstraint? Fill = null,
    string Display = "",
    bool Enabled = true,
    Action<string, int>? OnStep = null,
    Action<string, float>? OnDrag = null
)
{
    /// <summary>Converts a cursor position on the track to a 0..1 ratio.</summary>
    public float Ratio(int cursorX, int trackWidth)
    {
        if (trackWidth <= 2)
        {
            return 0f;
        }

        return Math.Clamp((cursorX - 1f) / (trackWidth - 2), 0f, 1f);
    }
}
