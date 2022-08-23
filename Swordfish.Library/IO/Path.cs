using System;
using System.IO;

namespace Swordfish.Library.IO
{
    public struct Path : IPath
    {
        public string OriginalString { get; set; }

        public Path(string value)
        {
            OriginalString = value;
        }

        public IPath At(string value)
        {
            return new Path(System.IO.Path.Combine(OriginalString, value));
        }

        public IPath At(params string[] values)
        {
            string[] joinedValues = new string[values.Length + 1];
            joinedValues[0] = OriginalString;
            values.CopyTo(joinedValues, 1);

            return new Path(System.IO.Path.Combine(joinedValues));
        }

        public IPath At(IPath path)
        {
            return new Path(System.IO.Path.Combine(OriginalString, path.ToString()));
        }

        public IPath CreateDirectory()
        {
            Directory.CreateDirectory(OriginalString);
            return this;
        }

        public override string ToString() => OriginalString;
    }
}
