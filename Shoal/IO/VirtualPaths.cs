namespace Shoal.IO;

public static class VirtualPaths
{
    public static PathInfo Lang => _lang ??= new PathInfo("lang/");
    public static PathInfo Config => _config ??= new PathInfo("config/");
    
    private static PathInfo? _lang;
    private static PathInfo? _config;
}
