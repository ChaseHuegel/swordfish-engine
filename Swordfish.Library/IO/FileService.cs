using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;

namespace Swordfish.Library.IO
{
    public class FileService : IFileService
    {
        private readonly ConcurrentSwitchDictionary<Type, string, IFileParser> Parsers;

        public FileService(IEnumerable<IFileParser> parsers)
        {
            Parsers = new ConcurrentSwitchDictionary<Type, string, IFileParser>();

            foreach (var parser in parsers)
            {
                foreach (var extension in parser.SupportedExtensions)
                {
                    Type interfaceType = parser.GetType().GetInterfaces()[0];
                    Type parserType = interfaceType.IsGenericType ? interfaceType.GenericTypeArguments[0] : parser.GetType();
                    Parsers.TryAdd(parserType, extension.ToLowerInvariant(), parser);
                }
            }
        }

        public Stream Open(PathInfo path)
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
                
                case "zip":
                    int zipPathLength = path.Value.IndexOf(".zip", StringComparison.Ordinal) + 4;
                    string zipFilePath = path.Value[..zipPathLength];
                    if (zipFilePath.Length == path.Value.Length)
                    {
                        throw new FileNotFoundException("Zip path does point include a file.", path.Value);
                    }

                    ZipArchive zip = ZipFile.OpenRead(zipFilePath);
                    string zipEntryPath = path.Value[(zipPathLength + 1)..];
                    ZipArchiveEntry entry = zip.GetEntry(zipEntryPath);
                    if (entry == null)
                    {
                        throw new FileNotFoundException("Zip entry not found.", path.Value);
                    }
                    
                    return new ZipArchiveEntryStream(zip, entry.Open());
            }

            return File.Open(path.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public byte[] ReadBytes(PathInfo path)
        {
            using (Stream stream = Open(path))
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public string ReadString(PathInfo path)
        {
            using (Stream stream = Open(path))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public void Write(PathInfo path, Stream stream)
        {
            using (Stream output = File.Open(path.Value, FileMode.OpenOrCreate, FileAccess.Write))
            {
                stream.CopyTo(output);
            }
        }

        public TResult Parse<TResult>(PathInfo path)
        {
            string extension = path.GetExtension();
            if (Parsers.TryGetValue(typeof(TResult), extension.ToLowerInvariant(), out IFileParser parser))
            {
                object parseResult = parser.Parse(this, path);
                return parseResult is TResult typedResult ? typedResult : default;
            }

            return default;
        }

        public bool TryParse<TResult>(PathInfo path, out TResult result)
        {
            string extension = path.GetExtension();
            if (Parsers.TryGetValue(typeof(TResult), extension.ToLowerInvariant(), out IFileParser parser))
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

        public PathInfo[] GetFiles(PathInfo path)
        {
            return GetFiles(path, "*", SearchOption.TopDirectoryOnly);
        }

        public PathInfo[] GetFiles(PathInfo path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public PathInfo[] GetFiles(PathInfo path, SearchOption searchOption)
        {
            return GetFiles(path, "*", searchOption);
        }

        public PathInfo[] GetFiles(PathInfo path, string searchPattern, SearchOption searchOption)
        {
            string dir = path.GetDirectory().Value;
            if (!Directory.Exists(dir))
            {
                return [];
            }
            
            return Directory.GetFiles(dir, searchPattern, searchOption)
                .Select(str => new PathInfo(str))
                .ToArray();
        }
    }
}
