using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Swordfish
{
    public class CoreSettings
    {
        public bool IS_RELEASE = false;

        public int PROFILE_LENGTH = 300;

        public string WINDOW_TITLE = "Swordfish Engine";
        public Vector2i WINDOW_SIZE = new Vector2i(1024, 768);
        public bool WINDOW_FULLSCREEN = false;

        public int FRAMELIMIT = 60;
        public float CLIP_NEAR = 0.1f;
        public float CLIP_FAR = 1000f;
    }
}