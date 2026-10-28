using Microsoft.CodeAnalysis;
using Xunit;

namespace Reef.Generator.Tests;

public class StyleResolverProbeTests
{
    [Fact]
    public void ParsesInheritance()
    {
        RssFile file = new RssParser("x.rss", ".a : .b { color: red; } .b : .a { color: blue; }").Parse();
        Assert.Equal("a", file.Rules[0].Selector);
        Assert.Equal("b", file.Rules[0].ParentSelector);
        var diagnostics = new List<Diagnostic>();
        var catalog = new StyleResolver(new List<RssFile> { file }, diagnostics).Resolve();
        Assert.True(catalog.TryGetAssignments("a", RssPseudoState.None, out _));
        Assert.Contains(diagnostics, d => d.Id == "RF0003");
    }
}
