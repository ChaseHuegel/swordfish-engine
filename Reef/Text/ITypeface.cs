namespace Reef.Text;

public interface ITypeface
{
    TextConstraints Measure(FontOptions fontOptions, string text, int start, int length);
    
    IntRect[] Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth);

    string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth);
}