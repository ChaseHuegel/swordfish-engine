namespace WaywardBeyond.Client.UI;

/// <summary>The outcome of one immediate-mode control: whether it changed, and its current value.</summary>
internal readonly record struct ControlResult<T>(bool Changed, T Value)
{
    /// <summary>Creates a result that reports no change and carries the given value.</summary>
    public static ControlResult<T> Unchanged(T value)
    {
        return new ControlResult<T>(false, value);
    }
}
