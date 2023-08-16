using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;

namespace Swordfish.Library.IO
{
    public class FileService : IFileService
    {
        private readonly ConcurrentSwitchDictionary<string, Type, IFileParser> Parsers;

        public FileService(IEnumerable<IFileParser> parsers)
        {
            Parsers = new ConcurrentSwitchDictionary<string, Type, IFileParser>();

            foreach (var parser in parsers)
            {
                foreach (var extension in parser.SupportedExtensions)
                {
                    Type interfaceType = parser.GetType().GetInterfaces()[0];
                    Type parserType = interfaceType.IsGenericType ? interfaceType.GenericTypeArguments[0] : parser.GetType();
                    Parsers.TryAdd(extension.ToLowerInvariant(), parserType, parser);
                }
            }
        }

        public Stream Open(IPath path)
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

            return File.Open(path.ToString(), FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public byte[] ReadBytes(IPath path)
        {
            using (Stream stream = Open(path))
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public string ReadString(IPath path)
        {
            using (Stream stream = Open(path))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
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
            if (Parsers.TryGetValue(extension.ToLowerInvariant(), typeof(TResult), out IFileParser parser))
            {
                object parseResult = parser.Parse(this, path);
                return parseResult is TResult typedResult ? typedResult : default;
            }

            return default;
        }

        public bool TryParse<TResult>(IPath path, out TResult result)
        {
            string extension = path.GetExtension();
            if (Parsers.TryGetValue(extension.ToLowerInvariant(), typeof(TResult), out IFileParser parser))
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

        public TResult Parse<TResult>(Stream stream)
        {
            throw new NotImplementedException();
        }

        public IPath[] GetFiles(IPath path)
        {
            return GetFiles(path, "*", SearchOption.TopDirectoryOnly);
        }

        public IPath[] GetFiles(IPath path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public IPath[] GetFiles(IPath path, SearchOption searchOption)
        {
            return GetFiles(path, "*", searchOption);
        }

        public IPath[] GetFiles(IPath path, string searchPattern, SearchOption searchOption)
        {
            string dir = path.GetDirectory().ToString();
            return Directory.GetFiles(dir, searchPattern, searchOption)
                .Select(str => (IPath)new Path(str))
                .ToArray();
        }
    }
}
