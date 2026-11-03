using Xunit;

namespace Reef.Generator.Tests;

public class ExpressionBinderDefectTests
{
    [Fact]
    public void QuotedArgumentKeepsContextPrefixText()
    {
        MarkupCompilation result = MarkupTestHost.Compile(
            """
            <Reef xmlns="Reef" Class="V" Context="State">
                <Text text="@(context.Label + '@ui.raw')" />
            </Reef>
            """,
            """
            namespace ReefMarkupTest
            {
                public sealed class State
                {
                    public string Label { get; set; } = string.Empty;
                }

                public partial class V
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains("\"@ui.raw\"", result.GeneratedText);
    }

    [Fact]
    public void GenericMethodResolvesFromTheArgumentType()
    {
        MarkupCompilation result = MarkupTestHost.Compile(
            """
            <Reef xmlns="Reef" Class="V" Context="State">
                <Text text="@context.Box.Echo('hello')" />
            </Reef>
            """,
            """
            namespace ReefMarkupTest
            {
                public sealed class Box
                {
                    public T Echo<T>(T value) => value;
                }

                public sealed class State
                {
                    public Box Box { get; set; } = new();
                }

                public partial class V
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains(".Echo(\"hello\")", result.GeneratedText);
    }

    [Fact]
    public void InheritedInterfaceMethodResolves()
    {
        MarkupCompilation result = MarkupTestHost.Compile(
            """
            <Reef xmlns="Reef" Class="V" Context="State">
                <Text text="@context.Shape.Describe()" />
            </Reef>
            """,
            """
            namespace ReefMarkupTest
            {
                public interface IBase
                {
                    string Describe();
                }

                public interface IShape : IBase
                {
                }

                public sealed class Shape : IShape
                {
                    public string Describe() => "ok";
                }

                public sealed class State
                {
                    public IShape Shape { get; set; } = new Shape();
                }

                public partial class V
                {
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains(".Describe()", result.GeneratedText);
    }

    [Fact]
    public void MixedNumericAdditionBinds()
    {
        MarkupCompilation result = MarkupTestHost.Compile(
            """
            <Reef xmlns="Reef" Class="V" Context="State">
                <Text text="@Format(context.Small + context.Big)" />
            </Reef>
            """,
            """
            namespace ReefMarkupTest
            {
                public sealed class State
                {
                    public int Small { get; set; }
                    public long Big { get; set; }
                }

                public partial class V
                {
                    protected string Format(long value) => value.ToString();
                }
            }
            """);
        Assert.Empty(result.Errors);
        Assert.Contains("Format(context.Small + context.Big)", result.GeneratedText);
    }
}
