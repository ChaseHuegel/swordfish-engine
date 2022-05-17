using OpenTK.Mathematics;

namespace Swordfish.Engine.ECS
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
        /// </summary>
        public float resistance;

        /// <summary>
        /// Restitution strength of this rigidbody
        /// </summary>
        public float restitution;

        /// <summary>
        /// Current velocity of the rigidbody in m/s
        /// </summary>
        public Vector3 velocity;

        /// <summary>
        /// Current acceleration of the rigidbody in m/s
        /// </summary>
        public Vector3 acceleration;

        /// <summary>
        /// Apply impulse to the rigidbody in m/s
        /// </summary>
        public Vector3 impulse;

        /// <summary>
        /// Dampens the rigidbody's velocity by m/s
        /// </summary>
        public float drag;
    }
}
