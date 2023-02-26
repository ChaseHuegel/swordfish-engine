using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Swordfish.Library.Extensions;

namespace Swordfish.Library.IO
{
    public class FileService : IFileService
    {
        private readonly ConcurrentDictionary<string, IFileParser> Parsers;

        public FileService(IEnumerable<IFileParser> parsers)
        {
            Parsers = new ConcurrentDictionary<string, IFileParser>(StringComparer.OrdinalIgnoreCase);

            foreach (var parser in parsers)
                foreach (var extension in parser.SupportedExtensions)
                    Parsers.TryAdd(extension, parser);
        }

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

        public TResult Parse<TResult>(IPath path)
        {
            string extension = path.GetExtension();
            if (Parsers.TryGetValue(extension, out IFileParser parser))
            {
                object parseResult = parser.Parse(this, path);
                return parseResult is TResult typedResult ? typedResult : default;
            }

            return default;
        }

        public bool TryParse<TResult>(IPath path, out TResult result)
        {
            string extension = path.GetExtension();
            if (Parsers.TryGetValue(extension, out IFileParser parser))
            {
                object parseResult = parser.Parse(this, path);

                if (parseResult is TResult typedResult)
                {
                    result = typedResult;
                    return true;
                }
                else
                {
                    result = default;
                    return false;
                }
            }

            result = default;
            return false;
        }

        public bool TryParse<TResult>(Stream stream, out TResult result)
        {
            throw new NotImplementedException();
        }
    }
}
