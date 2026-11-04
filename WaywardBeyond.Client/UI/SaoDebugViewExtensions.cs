namespace WaywardBeyond.Client.UI;

internal static class SaoDebugViewExtensions
{
    /// <summary>Advances to the next view in the F6 cycle.</summary>
    public static SaoDebugView Next(this SaoDebugView view) => view switch
    {
        SaoDebugView.Off => SaoDebugView.Blurred,
        SaoDebugView.Blurred => SaoDebugView.Raw,
        SaoDebugView.Raw => SaoDebugView.Depth,
        _ => SaoDebugView.Off,
    };
}
