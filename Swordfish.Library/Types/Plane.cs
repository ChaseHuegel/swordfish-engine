using OpenTK.Mathematics;

namespace Swordfish.Library.Types
{
    public struct Plane
    {
        public Vector3 Normal;
        public Vector3 Origin;

        /// <summary>
        /// Builds a view frustrum
        /// </summary>
        /// <param name="origin">frustrum location</param>
        /// <param name="forward">frustrum facing direction</param>
        /// <param name="up">frustrum up direction</param>
        /// <param name="right">frustrum right direction</param>
        /// <param name="fov">frustrum FOV in degrees</param>
        /// <param name="near">depth of the near plane</param>
        /// <param name="far">depth of the far plane</param>
        /// <returns>array of 6 planes that make up the frustrum</returns>
        public static Plane[] BuildViewFrustrum(Vector3 origin, Vector3 forward, Vector3 up, Vector3 right, float fov, float near, float far)
        {
            float fovOffset = fov * 0.8f;
            Plane[] planes = new Plane[6];

            //  Near/far plane
            planes[0] = new Plane() { Normal = forward, Origin = origin + (forward * near) };
            planes[1] = new Plane() { Normal = -forward, Origin = origin + (forward * far) };
            //  Side planes
            planes[2] = new Plane() { Normal = Quaternion.FromAxisAngle(up, MathHelper.DegreesToRadians(90f-fovOffset)) * forward, Origin = origin };
            planes[3] = new Plane() { Normal = Quaternion.FromAxisAngle(up, MathHelper.DegreesToRadians(270f+fovOffset)) * forward, Origin = origin };
            //  Top/bottom planes
            planes[4] = new Plane() { Normal = Quaternion.FromAxisAngle(right, MathHelper.DegreesToRadians(90f-fovOffset*0.8f)) * forward, Origin = origin };
            planes[5] = new Plane() { Normal = Quaternion.FromAxisAngle(right, MathHelper.DegreesToRadians(270f+fovOffset*0.8f)) * forward, Origin = origin };

            return planes;
        }
    }
}
