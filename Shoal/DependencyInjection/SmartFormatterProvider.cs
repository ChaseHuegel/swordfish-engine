using SmartFormat.Core.Settings;

namespace Shoal.DependencyInjection;

internal static class SmartFormatterProvider
{
    [ThreadStatic]
    private static SmartFormatter? _smartFormatter;

    public static SmartFormatter Resolve(IResolverContext context)
    {
        if (_smartFormatter != null)
        {
            return _smartFormatter;
        }

        SmartSettings settings = new() 
        {
            Localization = 
            {
                LocalizationProvider = context.Resolve<ILocalizationProvider>(),
            },
        };

        _smartFormatter = Smart.CreateDefaultSmartFormat(settings);

        foreach (IFormatter? formatter in context.ResolveMany<IFormatter>())
        {
            _smartFormatter.AddExtensions(formatter);
        }

        foreach (ISource? source in context.ResolveMany<ISource>())
        {
            _smartFormatter.AddExtensions(source);
        }

        return _smartFormatter;
    }
}