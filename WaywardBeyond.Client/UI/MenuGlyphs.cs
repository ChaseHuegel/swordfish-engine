namespace WaywardBeyond.Client.UI;

/// <summary>Font Awesome glyphs used by menu widgets.</summary>
internal static class MenuGlyphs
{
    public const string DICE = "\uf074";
    public const string TRASH = "\uf2ed";
    public const string CREW = "\uf0c0";
    public const string SERVER = "\uf233";
    public const string CHEVRON_LEFT = "\uf053";
    public const string CHEVRON_RIGHT = "\uf054";
    public const string STAR = "\uf005";
    public const string SIGNAL = "\uf012";
    public const string X_MARK = "\uf00d";
    public const string FEEDBACK = "\uf086";
    public const string SETTINGS = "\uf013";
    public const string ROTATE = "\uf021";
    public const string SHAPES = "\uf61f";
    public const string DISCORD = "\uf392";
    public const string STEAM = "\uf1b6";

    /// <summary>Returns the glyph constant for a semantic name.</summary>
    public static string Get(string name)
    {
        return name switch
        {
            "dice" => DICE,
            "trash" => TRASH,
            "crew" => CREW,
            "server" => SERVER,
            "chevron-left" => CHEVRON_LEFT,
            "chevron-right" => CHEVRON_RIGHT,
            "star" => STAR,
            "signal" => SIGNAL,
            "x-mark" => X_MARK,
            "feedback" => FEEDBACK,
            "settings" => SETTINGS,
            "rotate" => ROTATE,
            "shapes" => SHAPES,
            "discord" => DISCORD,
            "steam" => STEAM,
            _ => string.Empty,
        };
    }
}
