namespace Reef.Text;

public struct TextConstraints(int minWidth, int minHeight, int preferredWidth, int preferredHeight)
{
    public int MinWidth = minWidth;
    public int MinHeight = minHeight;
    public int PreferredWidth = preferredWidth;
    public int PreferredHeight = preferredHeight;
}