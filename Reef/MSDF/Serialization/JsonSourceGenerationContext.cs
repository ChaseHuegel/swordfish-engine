using System.Text.Json.Serialization;
using Reef.MSDF.Models;

namespace Reef.MSDF.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GlyphAtlas))]
internal partial class JsonSourceGenerationContext : JsonSerializerContext
{
    
}