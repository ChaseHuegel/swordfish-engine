using OpenTK.Mathematics;

namespace Swordfish.Rendering
{
    public class Camera
    {
        public Transform transform;
        public Matrix4 view;

        public float FOV;

        public Camera(Vector3 position, Vector3 rotation, float fov = 70f)
        {
            this.FOV = fov;
            this.transform = new Transform(position, rotation);

            UpdateView();
        }

        public void UpdateView()
        {
            view = transform.GetInverseMatrix();
        }

        public void Update()
        {
            UpdateView();
        }
    }
}