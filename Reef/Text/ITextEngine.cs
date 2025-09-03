using Reef.UI;

namespace Reef.Text;

public interface ITextEngine
{
    ITypeface GetDefaultTypeface();
    
    ITypeface[] GetTypefaces();
    
    bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface);
    
    TextConstraints Measure(FontOptions fontOptions, string text);
    TextConstraints Measure(FontOptions fontOptions, string text, int start, int length);
    
    TextLayout Layout(FontOptions fontOptions, string text);
    TextLayout Layout(FontOptions fontOptions, string text, int maxWidth);
    TextLayout Layout(FontOptions fontOptions, string text, int start, int length);
    TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth);

    string[] Wrap(FontOptions fontOptions, string text, int maxWidth);
    string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth);
}