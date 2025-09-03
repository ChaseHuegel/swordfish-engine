using Reef.UI;

namespace Reef.Text;

public interface ITypeface
{
    string ID { get; }
    
    AtlasInfo GetAtlasInfo();
    
    TextConstraints Measure(FontOptions fontOptions, string text, int start, int length);
    
    TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth);

    string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth);
}