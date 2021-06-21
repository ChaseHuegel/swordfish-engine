using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    /// <summary>
    /// A rigidbody acted on by physics
    /// </summary>
    [Component]
    public struct RigidbodyComponent
    {
        /// <summary>
        /// Mass of the rigidbody in kg
        /// </summary>
        public float mass;

        /// <summary>
        /// Air resistance of the rigidbody when in freefall
        /// <para>value of 1 is full resistance, 0 is no resistance. Negative values decrease resistance.</para>
        /// </summary>
        public float resistance;

        /// <summary>
        /// Surface friction of the rigidbody when in contact with another rigidbody
        /// <para>value of 1 is full friction, 0 is no friction. Negative values decrease friction.</para>
        /// </summary>
        public float friction;

        /// <summary>
        /// Restitution strength of this rigidbody
        /// <para>value is ranged 0-1</para>
        /// </summary>
        public float restitution;

        /// <summary>
        /// Current velocity of the rigidbody in m/s
        /// </summary>
        public Vector3 velocity;

        /// <summary>
        /// Dampens the rigidbody's velocity by m/s
        /// </summary>
        public float drag;
    }
}
