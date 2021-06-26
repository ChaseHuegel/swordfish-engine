using System;
using System.IO;

using OpenTK.Windowing.Common;

using Swordfish.Diagnostics;

using Tomlet;

namespace Swordfish
{
    public class CoreSettings
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
            public int HISTORY = 300;
        }

        public RendererSettings Renderer = new RendererSettings();
        public class RendererSettings
        {
            public float CLIP_NEAR = 0.1f;
            public float CLIP_FAR = 1000f;
        }

        public PhysicsSettings Physics = new PhysicsSettings();
        public class PhysicsSettings
        {
            public float FIXED_TIMESTEP = 0.016f;
        }

        public CoreSettings() {}

        /// <summary>
        /// Creates an instance of CoreSettings from a TOML config file
        /// </summary>
        /// <param name="path">path to the config including name and exension</param>
        /// <returns>instance of CoreSettings from the config file; otherwise default CoreSettings if config failed to load</returns>
        public static CoreSettings LoadConfig(string path)
        {
            string tomlString = "";
            CoreSettings settings = new CoreSettings();

            Debug.Log($"Loading core config from '{path}' ...");

            try
            {
                tomlString = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.Log(e.Message, LogType.ERROR);

                if (e is FileNotFoundException)
                {
                    tomlString = TomletMain.DocumentFrom<CoreSettings>(settings).SerializedValue;
                    File.WriteAllText(path, tomlString);

                    Debug.Log($"...Created file from default at '{Path.GetFileName(path)}'");
                }
            }

            try
            {
                settings = TomletMain.To<CoreSettings>(tomlString);

                Debug.Log($"Loaded core config.");
            }
            catch (Exception e)
            {
                Debug.Log(e.Message, LogType.ERROR);

                Debug.Log($"Falling back to default core config.");
            }

            return settings;
        }
    }
}