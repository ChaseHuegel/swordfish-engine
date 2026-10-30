using System;
using Reef;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Invokes a caller-supplied body inside the settings pane.</summary>
internal static class SettingsBodyWidget
{
    /// <summary>Draws the pane body.</summary>
    public static void SettingsBody(this UIBuilder<Material> ui, Action<UIBuilder<Material>>? body)
    {
        body?.Invoke(ui);
    }
}
