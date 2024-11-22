using System.Collections.Generic;
using Shoal.Globalization;
using SmartFormat;
using SmartFormat.Extensions;
using Swordfish.Library.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

// ReSharper disable once UnusedType.Global
public class LocalizationTests(ITestOutputHelper output) : TestBase(output)
{
    protected override void OnSetup()
    {
        var enLang = new Language("en");
        enLang.Translations.Add("Test.KVP.Pair", "{Key}={Value}");
        enLang.Translations.Add("Test.KVP.Key", "Key");
        enLang.Translations.Add("Test.KVP.Value", "Value");

        Language[] languages =
        [
            enLang,
        ];

        Smart.Default.Settings.Localization.LocalizationProvider = new Localization(languages);
        Smart.Default.AddExtensions(new LocalizationFormatter());
    }

    protected override void OnTearDown()
    {
        Smart.Default.Settings.Localization.LocalizationProvider = null;
    }

    [Fact]
    public void CanFormatLanguages()
    {
        var kvp = new KeyValuePair<string ,string>("hello", "world");

        string formattedKVP = Smart.Format("{:L:Test.KVP.Pair}", kvp);
        string formattedKey = Smart.Format("{:L:Test.KVP.Key}", kvp);
        string formattedValue = Smart.Format("{:L:Test.KVP.Value}", kvp);
        
        Assert.Equal($"{kvp.Key}={kvp.Value}", formattedKVP);
        Assert.Equal("Key", formattedKey);
        Assert.Equal("Value", formattedValue);
    }
}