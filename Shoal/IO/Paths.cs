namespace Shoal.IO;

public static class Paths
{
    public static PathInfo Config => _config ??= new PathInfo("config/");
    public static PathInfo Modules => _modules ??= new PathInfo("modules/");
    
    private static PathInfo? _config;
    private static PathInfo? _modules;
}
