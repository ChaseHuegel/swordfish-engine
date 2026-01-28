using System.Text;

namespace WaywardBeyond.Client.Core.UI;

internal struct TextBoxState(in string initialValue, in string? placeholder = null)
{
    public int CaretIndex;
    public int SelectionStartIndex;
    
    public readonly StringBuilder Text = new(initialValue);
    public readonly string? PlaceholderText = placeholder;
}