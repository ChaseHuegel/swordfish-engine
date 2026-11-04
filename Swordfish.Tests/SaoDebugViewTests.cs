using WaywardBeyond.Client.UI;
using Xunit;

namespace Swordfish.Tests;

public class SaoDebugViewTests
{
    [Fact]
    public void Next_CyclesThroughAllViews()
    {
        var view = SaoDebugView.Off;

        view = view.Next();
        Assert.Equal(SaoDebugView.Blurred, view);

        view = view.Next();
        Assert.Equal(SaoDebugView.Raw, view);

        view = view.Next();
        Assert.Equal(SaoDebugView.Depth, view);

        view = view.Next();
        Assert.Equal(SaoDebugView.Off, view);
    }
}
