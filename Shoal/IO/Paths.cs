namespace Shoal.IO;

public static class Paths
{
    private static PathInfo? _assets;
    private static PathInfo? _lang;
    private static PathInfo? _config;
    private static PathInfo? _modules;

    public static PathInfo Assets => _assets ??= new PathInfo("assets/");
    public static PathInfo Config => _config ??= Assets.At("config/");
    public static PathInfo Lang => _lang ??= Assets.At("lang/");
    public static PathInfo Modules => _modules ??= Assets.At("modules/");
}