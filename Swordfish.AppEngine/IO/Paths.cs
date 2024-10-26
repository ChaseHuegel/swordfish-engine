namespace Swordfish.AppEngine.IO;

public static class Paths
{
    private static IPath? _assets;
    private static IPath? _lang;
    private static IPath? _config;
    private static IPath? _modules;

    public static IPath Assets => _assets ??= new Path("assets/");
    public static IPath Config => _config ??= Assets.At("config/");
    public static IPath Lang => _lang ??= Assets.At("lang/");
    public static IPath Modules => _modules ??= Assets.At("modules/");
}