using System;
using System.Collections.Generic;
using System.IO;
using Swordfish.Library.Collections;

namespace Swordfish.Library.IO
{
    public class FileParseService : IFileParseService
    {
        private readonly ConcurrentSwitchDictionary<Type, string, IFileParser> _parsers;

        public FileParseService(IEnumerable<IFileParser> parsers)
        {
            _parsers = new ConcurrentSwitchDictionary<Type, string, IFileParser>();

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
            string extension = path.GetExtension();
            if (_parsers.TryGetValue(typeof(TResult), extension.ToLowerInvariant(), out IFileParser parser))
            {
                object parseResult = parser.Parse(path);
                return parseResult is TResult typedResult ? typedResult : default;
            }

            return default;
        }

        public bool TryParse<TResult>(PathInfo path, out TResult result)
        {
            string extension = path.GetExtension();
            if (_parsers.TryGetValue(typeof(TResult), extension.ToLowerInvariant(), out IFileParser parser))
            {
                object parseResult = parser.Parse(path);

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
