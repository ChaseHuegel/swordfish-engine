namespace Reef.Text;

public interface ITextEngine
{
    TextConstraints Measure(FontOptions fontOptions, string text);
    TextConstraints Measure(FontOptions fontOptions, string text, int start, int length);
    
    IntRect[] Layout(FontOptions fontOptions, string text);
    IntRect[] Layout(FontOptions fontOptions, string text, int maxWidth);
    IntRect[] Layout(FontOptions fontOptions, string text, int start, int length);
    IntRect[] Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth);
}