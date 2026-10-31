using System.Numerics;
using Swordfish.Library.Types.Shapes;
using Xunit;

namespace Swordfish.Tests;

public class ShapeEqualityTests
{
    [Fact]
    public void ValuesInsideTheQuantizationStepAreEqualAndHashEqual()
    {
        Shape left = new Box3(new Vector3(1f, 2f, 3f));
        Shape right = new Box3(new Vector3(1.00001f, 2.00001f, 3.00001f));

        Assert.True(left.Equals(right));
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ValuesOutsideTheQuantizationStepAreNotEqual()
    {
        Shape left = new Box3(new Vector3(1f, 2f, 3f));
        Shape right = new Box3(new Vector3(1.01f, 2f, 3f));

        Assert.False(left.Equals(right));
        Assert.True(left != right);
    }

    [Fact]
    public void SphereValuesInsideTheQuantizationStepAreEqualAndHashEqual()
    {
        Shape left = new Sphere(1f);
        Shape right = new Sphere(1.00001f);

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void DifferentShapeTypesAreNotEqual()
    {
        Shape box = new Box3(Vector3.One);
        Shape sphere = new Sphere(1f);

        Assert.False(box.Equals(sphere));
        Assert.NotEqual(box.GetHashCode(), sphere.GetHashCode());
    }
}
