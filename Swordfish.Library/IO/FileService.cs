using System.IO;
using System.Reflection;
using System.Text;
using Swordfish.Library.Extensions;

namespace Swordfish.Library.IO
{
    public class FileService : IFileService
    {
        public Stream Read(IPath path)
        {
            switch (path.Scheme)
            {
                case "manifest":
                    Assembly assembly = Assembly.GetCallingAssembly();
                    string assemblyName = assembly.GetName().Name;

                    const string manifestRoot = ".Manifest.";
                    StringBuilder builder = new StringBuilder(
                        assemblyName.Length + manifestRoot.Length + path.Value.Length
                    );

                    builder.Append(assemblyName);
                    builder.Append(manifestRoot);
                    builder.Append(path.Value.Substitute('.', '/', '\\'));

                    return assembly.GetManifestResourceStream(builder.ToString());
            }

            return File.Open(path.ToString(), FileMode.Open, FileAccess.Read);
        }

        public void Write(IPath path, Stream stream)
        {
            using (Stream output = File.Open(path.ToString(), FileMode.OpenOrCreate, FileAccess.Write))
            {
                stream.CopyTo(output);
            }
        }
    }
}
