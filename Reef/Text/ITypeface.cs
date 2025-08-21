using Reef.UI;

namespace Reef.Text;

public interface ITypeface
{
    AtlasInfo GetAtlasInfo();
    
    TextConstraints Measure(FontOptions fontOptions, string text, int start, int length);
    
    TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth);

    string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth);
}