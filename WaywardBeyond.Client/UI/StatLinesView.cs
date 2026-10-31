using System.Collections.Generic;

namespace WaywardBeyond.Client.UI;

/// <summary>One preformatted debug stat line.</summary>
internal readonly record struct StatLine(string Text, bool Danger = false, bool Warning = false);

/// <summary>Display data for a debug stat block.</summary>
internal readonly record struct StatLinesState(IReadOnlyList<StatLine> Lines);

/// <summary>Draws preformatted debug stat lines.</summary>
internal partial class StatLinesView
{
}
