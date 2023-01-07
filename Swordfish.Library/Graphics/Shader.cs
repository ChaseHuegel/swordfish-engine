using System.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Types;

namespace Swordfish.Library.Graphics
{
    public class Shader : IAsset
    {
        public string Name { get; set; }
        public IPath Path { get; }
        public string Source { get; }

        private bool m_Disposed;

        public Shader(string name, IPath path, IFileService fileService = null)
        {
            Name = name;
            Path = path;
            using (StreamReader reader = new StreamReader(fileService.Read(path)))
            {
                Source = reader.ReadToEnd();
            }
        }

        public Shader(string name, string source)
        {
            Name = name;
            Source = source;
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;

            m_Disposed = true;
        }
    }
}
