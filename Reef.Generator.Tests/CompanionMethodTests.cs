using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Reef.Generator.Tests;

public class CompanionMethodTests
{
    [Fact]
    public void DetectsPartialImplementation()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            public partial class X
            {
                partial void M()
                {
                }
            }
            """);
        var compilation = CSharpCompilation.Create(
            "Probe",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("X")!;
        IMethodSymbol method = (IMethodSymbol)type.GetMembers("M").Single();
        Assert.True(CompanionMethods.IsPartialImplementation(method), method.ToDisplayString() + " / " + method.DeclaringSyntaxReferences.Length);
    }
}
