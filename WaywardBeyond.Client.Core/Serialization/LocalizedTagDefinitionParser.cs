using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using Swordfish.Library.IO;
using Tomlet;
using WaywardBeyond.Client.Core.Meta;

namespace WaywardBeyond.Client.Core.Serialization;

internal class LocalizedTagDefinitionParser : IFileParser<LocalizedTagsDefinition>
{
    public string[] SupportedExtensions { get; } =
    [
        ".csv",
        ".toml",
    ];
    
    object IFileParser.Parse(PathInfo path) => Parse(path);
    public LocalizedTagsDefinition Parse(PathInfo file)
    {
        if (file.GetExtension() == ".toml")
        {
            return TomletMain.To<LocalizedTagsDefinition>(file.ReadString());
        }

        string fileName = file.GetFileNameWithoutExtension();
        string[] fileNameParts = fileName.Split('.');
        if (fileNameParts.Length < 2)
        {
            throw new InvalidOperationException("CSV tag files must have a two letter ISO code at the end of its name, separated by a '.'");
        }

        string langCode = fileNameParts[^1];
        if (langCode.Length != 2)
        {
            throw new InvalidOperationException("CSV tag files must have a two letter ISO code at the end of its name, separated by a '.'");
        }
        
        using var reader = new StreamReader(file);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        csv.Read();
        csv.ReadHeader();
        
        string[]? headers = csv.HeaderRecord;
        if (headers == null)
        {
            throw new NotSupportedException("CSV tag file must have a header row.");
        }

        var columns = new Dictionary<string, List<string>>();
        foreach (string header in headers)
        {
            columns[header] = [];
        }

        while (csv.Read())
        {
            foreach (string header in headers)
            {
                var value = csv.GetField<string>(header);
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                
                columns[header].Add(value);
            }
        }

        return new LocalizedTagsDefinition(langCode, columns);
    }
}