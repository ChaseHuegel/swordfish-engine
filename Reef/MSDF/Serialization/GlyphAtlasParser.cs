using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using Reef.MSDF.Models;

namespace Reef.MSDF.Serialization;

internal static class GlyphAtlasParser
{
    public static GlyphAtlas Parse(string path)
    {
        using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var atlas = JsonSerializer.Deserialize<GlyphAtlas>(fs, JsonSourceGenerationContext.Default.GlyphAtlas);
        if (atlas == null)
        {
            throw new JsonException($"Failed to deserialize a valid {nameof(GlyphAtlas)}.");
        }

        return atlas;
    }
    
    public static bool TryParse(string path, [NotNullWhen(true)] out GlyphAtlas? glyphAtlas)
    {
        try
        {
            glyphAtlas = Parse(path);
            return true;
        }
        catch
        {
            glyphAtlas = null;
            return false;
        }
    }
}