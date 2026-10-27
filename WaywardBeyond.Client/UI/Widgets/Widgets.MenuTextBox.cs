using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Services;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a clipped menu text box within a fixed rectangle and returns its interactions.</summary>
    /// <param name="soundEffectService">Plays the typing and submit sounds when provided.</param>
    public static Interactions MenuTextBox(
        this UIBuilder<Material> ui,
        string id,
        ref TextBoxState state,
        IntRect rect,
        FontOptions fontOptions,
        IInputService inputService,
        SoundEffectService? soundEffectService = null
    )
    {
        using (ui.Element())
        {
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(rect.Size.X),
                Height = new Fixed(rect.Size.Y),
            };
            ui.TextBox(id, ref state, fontOptions, inputService, out Interactions interactions);
            if (soundEffectService != null)
            {
                interactions.WithTextInputAudio(soundEffectService);
            }

            return interactions;
        }
    }
}
