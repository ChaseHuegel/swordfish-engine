using System;
using System.Collections.Generic;
using System.IO;
using Swordfish.Library.Collections;

namespace Swordfish.Library.IO
{
    public class VirtualFileParseService : IFileParseService
    {
        private readonly ConcurrentSwitchDictionary<Type, string, IFileParser> _parsers;
        private readonly VirtualFileSystem _vfs;

        public VirtualFileParseService(IEnumerable<IFileParser> parsers, VirtualFileSystem vfs)
        {
            _parsers = new ConcurrentSwitchDictionary<Type, string, IFileParser>();
            _vfs = vfs;

            foreach (IFileParser parser in parsers)
            {
                foreach (string extension in parser.SupportedExtensions)
                {
                    Type interfaceType = parser.GetType().GetInterfaces()[0];
                    Type parserType = interfaceType.IsGenericType ? interfaceType.GenericTypeArguments[0] : parser.GetType();
                    _parsers.TryAdd(parserType, extension.ToLowerInvariant(), parser);
                }
            }
        }

        public TResult Parse<TResult>(PathInfo path)
        {
            if (!_vfs.TryResolvePath(path, out PathInfo finalPath))
            {
                finalPath = path;
            }
            
            string extension = finalPath.GetExtension();
            if (!_parsers.TryGetValue(typeof(TResult), extension.ToLowerInvariant(), out IFileParser parser))
            {
                return default;
            }

            object parseResult = parser.Parse(finalPath);
            return parseResult is TResult typedResult ? typedResult : default;
        }

        public bool TryParse<TResult>(PathInfo path, out TResult result)
        {
            if (!_vfs.TryResolvePath(path, out PathInfo finalPath))
            {
                finalPath = path;
            }
            
            string extension = finalPath.GetExtension();
            if (_parsers.TryGetValue(typeof(TResult), extension.ToLowerInvariant(), out IFileParser parser))
            {
                object parseResult = parser.Parse(finalPath);

                if (parseResult is TResult typedResult)
                {
                    result = typedResult;
                    return true;
                }

                result = default;
                return false;
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
    }
}
