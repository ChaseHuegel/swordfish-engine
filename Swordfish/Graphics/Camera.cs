using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public class Camera
{
    public Transform Transform { get; set; } = new();

    public int FOV { get; set; } = 60;

    public Camera() { }

    public Camera(int fov)
    {
        FOV = fov;
    }
}
