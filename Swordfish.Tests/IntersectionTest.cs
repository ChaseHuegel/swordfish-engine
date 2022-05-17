using OpenTK.Mathematics;

using Swordfish.Library.Util;

using Xunit;

namespace SwordfishTests
{
    public class IntersectionTest
    {
        [Fact]
        public void SphereToBoundryIsColliding()
        {
            bool colliding = Intersection.SphereToPlane(
                new Vector3(0f, 0f, 0f), 0.5f,
                new Vector3(0f, 1f, 0f), Vector3.Zero
            );

            Assert.True(colliding);
        }

        [Fact]
        public void SphereToBoundryIsNotColliding()
        {
            bool colliding = Intersection.SphereToPlane(
                new Vector3(10f, 1f, -7f), 0.5f,
                new Vector3(0f, 1f, 0f), Vector3.Zero
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
