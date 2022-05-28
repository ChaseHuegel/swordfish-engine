using System;
using System.IO;
using Swordfish.Library.Diagnostics;
using Tomlet;

namespace Swordfish.Library.Util
{
    public class Config
    {
        /// <summary>
        /// Creates a <see cref="Config"/> instance of type T from a TOML config file
        /// </summary>
        /// <param name="path">path to the config including name and exension</param>
        /// <returns>instance of type T from the config file; otherwise default of type T if config failed to load</returns>
        public static T Load<T>(string path) where T : Config
        {
            string tomlString = "";
            T config = Activator.CreateInstance<T>();
            Debug.Log($"Loading {typeof(T).Name} from '{path}' ...");

            try
            {
                tomlString = File.ReadAllText(path);
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                tomlString = TomletMain.DocumentFrom<T>(config).SerializedValue;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, tomlString);
                Debug.Log($"...{typeof(T).Name} was not found, created from default at '{Path.GetFileName(path)}'", LogType.WARNING);
            }
            catch (Exception e)
            {
                Debug.Log(e.Message, LogType.ERROR);
            }

            try
            {
                config = TomletMain.To<T>(tomlString);
                Debug.Log($"Loaded {typeof(T).Name}.");
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                Debug.Log($"Falling back to default {typeof(T).Name}.", LogType.WARNING);
            }

            return config;
        }
    }
}
