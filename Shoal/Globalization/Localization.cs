namespace Shoal.Globalization;

internal class Localization : ILocalizationProvider, ILocalization
{
    private readonly Dictionary<string, Language> _languages = [];

    public Localization(IReadOnlyCollection<Language> languageDefinitions)
    {
        foreach (Language languageDefinition in languageDefinitions)
        {
            string key = languageDefinition.TwoLetterISOLanguageName;
            if (_languages.TryGetValue(key, out Language? language))
            {
                foreach (KeyValuePair<string, string> translation in languageDefinition.Translations)
                {
                    language.Translations[translation.Key] = translation.Value;
                }
            }
            else
            {
                _languages.Add(key, languageDefinition);
            }
        }
    }
    
    public string? GetString(string value)
    {
        return GetTranslation(value, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    public string? GetString(string value, string cultureName)
    {
        return GetTranslation(value, cultureName);
    }

    public string? GetString(string value, CultureInfo cultureInfo)
    {
        return GetTranslation(value, cultureInfo.TwoLetterISOLanguageName);
    }

    private string? GetTranslation(string value, string cultureName)
    {
        return _languages.TryGetValue(cultureName, out Language? language) ? language.Translations.GetValueOrDefault(value) : null;
    }
}