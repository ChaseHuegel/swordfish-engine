using System;
using System.Diagnostics;
using System.IO;
using Swordfish.Library.Annotations;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Library.IO
{
    public struct Path : IPath
    {
        public string OriginalString { get; }

        public string Value { get; set; }

        [NotNull]
        public string Scheme { get; }

        public Path(string value)
        {
            OriginalString = value;

            int schemeEndIndex = value.IndexOf("://");
            Value = schemeEndIndex > 0 ? value.Substring(schemeEndIndex + 3) : value;
            Scheme = schemeEndIndex > 0 ? value.Substring(0, schemeEndIndex).ToLowerInvariant() : string.Empty;
        }

        public IPath At(string value)
        {
            return new Path(System.IO.Path.Combine(Scheme, Value, value));
        }

        public IPath At(params string[] values)
        {
            string[] joinedValues = new string[values.Length + 2];
            joinedValues[0] = Scheme;
            joinedValues[1] = OriginalString;
            values.CopyTo(joinedValues, 1);

            return new Path(System.IO.Path.Combine(joinedValues));
        }

        public IPath At(IPath path)
        {
            return new Path(System.IO.Path.Combine(Scheme, Value, path.ToString()));
        }

        public IPath GetDirectory()
        {
            return new Path(System.IO.Path.GetDirectoryName(OriginalString));
        }

        public IPath CreateDirectory()
        {
            Directory.CreateDirectory(Value);
            return this;
        }

        public string GetFileName()
        {
            return System.IO.Path.GetFileName(OriginalString);
        }

        public string GetFileNameWithoutExtension()
        {
            return System.IO.Path.GetFileNameWithoutExtension(OriginalString);
        }

        public string GetExtension()
        {
            return System.IO.Path.GetExtension(OriginalString);
        }

        public string GetDirectoryName()
        {
            return System.IO.Path.GetDirectoryName(OriginalString);
        }

        public bool TryOpenInDefaultApp()
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = OriginalString,
                UseShellExecute = true
            };

            return Debugger.TryInvoke(
                () => Process.Start(processStartInfo),
                "Failed to open path in the default application."
            );
        }

        public override string ToString() => OriginalString;
    }
}
