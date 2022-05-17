using OpenTK.Windowing.Common;

using Swordfish.Library.Types;
using Swordfish.Library.Util;

namespace Swordfish.Engine
{
    public class CoreSettings : Config
    {
        public WindowSettings Window = new WindowSettings();
        public class WindowSettings
        {
            public string TITLE = "Swordfish Engine";
            public bool FULLSCREEN = false;

            public int WIDTH = 1024;
            public int HEIGHT = 768;

            public int FRAMELIMIT = 120;
            public int UPDATELIMIT = 0;
            public VSyncMode VSYNC = VSyncMode.Off;
        }

        public ProfilerSettings Profiler = new ProfilerSettings();
        public class ProfilerSettings
        {
            public int HISTORY {
                get => Library.Diagnostics.Profiler.HistoryLength;
                set => Library.Diagnostics.Profiler.HistoryLength = value;
            }
        }

        public RendererSettings Renderer = new RendererSettings();
        public class RendererSettings
        {
            public float CLIP_NEAR = 0.1f;
            public float CLIP_FAR = 1000f;

            public bool WIREFRAME = false;

            public float EXPOSURE = 1.0f;
            public Color BACKGROUND_COLOR = new Color(0.08f, 0.1f, 0.14f, 1.0f);
        }

        public PhysicsSettings Physics = new PhysicsSettings();
        public class PhysicsSettings
        {
            public float FIXED_TIMESTEP = 0.016f;
            public float MAX_TIMESTEP = 0.1f;
            public bool ACCUMULATE_TIMESTEPS = true;
        }
    }
}