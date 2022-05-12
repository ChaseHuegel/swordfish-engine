using OpenTK.Mathematics;

namespace Swordfish.Engine.Rendering
{
    public class Camera
    {
        public static Camera Main = null;

        public Transform transform;
        public Matrix4 view;

        public float FOV;

        public Camera(Vector3 position, Vector3 rotation, float fov = 70f)
        {
            if (Main == null) Main = this;

            this.FOV = fov;
            this.transform = new Transform(position, rotation);

            UpdateView();
        }

        public void UpdateView()
        {
            view = transform.GetMatrix().Inverted();
        }

        public void Update()
        {
            UpdateView();
        }
    }
}