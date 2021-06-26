using Xunit;
using Swordfish.Physics;
using OpenTK.Mathematics;

namespace SwordfishTests
{
    public class IntersectionTest
    {
        [Fact]
        public void SphereToBoundryIsColliding()
        {
            bool colliding = Intersection.SphereToBoundry(
                new Vector3(0f, 0f, 0f), 0.5f,
                new Vector3(0f, 1f, 0f), 0f
            );

            Assert.True(colliding);
        }

        [Fact]
        public void SphereToBoundryIsNotColliding()
        {
            bool colliding = Intersection.SphereToBoundry(
                new Vector3(10f, 1f, -7f), 0.5f,
                new Vector3(0f, 1f, 0f), 0f
            );

            Assert.False(colliding);
        }

        [Fact]
        public void SphereToSphereIsColliding()
        {
            bool colliding = Intersection.SphereToSphere(
                new Vector3(0f, 0f, 0f), 0.5f,
                new Vector3(0f, 0.5f, 0f), 0.5f
            );

            Assert.True(colliding);
        }

        [Fact]
        public void SphereToSphereIsNotColliding()
        {
            bool colliding = Intersection.SphereToSphere(
                new Vector3(0f, 0f, 0f), 0.5f,
                new Vector3(0f, 1.1f, 0f), 0.5f
            );

            Assert.False(colliding);
        }
    }
}
