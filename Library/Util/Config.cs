using System;
using System.IO;

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
            Console.WriteLine($"Loading {typeof(T).Name} from '{path}' ...");

            try
            {
                tomlString = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);

                if (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    tomlString = TomletMain.DocumentFrom<T>(config).SerializedValue;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, tomlString);
                    Console.WriteLine($"...Created file from default {typeof(T).Name} at '{Path.GetFileName(path)}'");
                }
            }

            try
            {
                config = TomletMain.To<T>(tomlString);
                Console.WriteLine($"Loaded {typeof(T).Name}.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine($"Falling back to default {typeof(T).Name}.");
            }

            return config;
        }
    }
}
