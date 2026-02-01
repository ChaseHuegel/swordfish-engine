using System.Text;

namespace WaywardBeyond.Client.Core.UI;

internal struct TextBoxState(in string initialValue, in string? placeholder = null)
{
    public bool Selecting;
    public int CaretIndex;
    public int SelectionStartIndex;
    
    public readonly StringBuilder Text = new(initialValue);
    public readonly string? PlaceholderText = placeholder;

    /// <summary>
    ///     Determines whether any text is selected.
    /// </summary>
    public bool HasSelection()
    {
        return SelectionStartIndex != CaretIndex;
    }
    
    /// <summary>
    ///     Calculates the index, length, and direction of the selection.
    /// </summary>
    public Selection CalculateSelection()
    {
        bool forward;
        int selectionStartIndex;
        int selectionLength;
        if (SelectionStartIndex < CaretIndex)
        {
            selectionStartIndex = SelectionStartIndex;
            selectionLength = CaretIndex - SelectionStartIndex;
            forward = true;
        }
        else
        {
            selectionStartIndex = CaretIndex;
            selectionLength = SelectionStartIndex - CaretIndex;
            forward = false;
        }
        
        return new Selection(selectionStartIndex, selectionLength, forward);
    }

    public readonly record struct Selection(int StartIndex, int Length, bool Forward);
}