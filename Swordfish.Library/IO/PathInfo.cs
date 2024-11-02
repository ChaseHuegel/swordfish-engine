using System;
using System.Diagnostics;
using System.IO;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Library.IO
{
    public readonly struct PathInfo
    {
        public string OriginalString { get; }

        public string Value { get; }

        public string Scheme { get; }

        public PathInfo(string value)
        {
            OriginalString = value;

            int schemeEndIndex = value.IndexOf("://", StringComparison.Ordinal);
            Value = schemeEndIndex > 0 ? value[(schemeEndIndex + 3)..] : value;
            Scheme = schemeEndIndex > 0 ? value[..schemeEndIndex].ToLowerInvariant() : string.Empty;
        }

        public PathInfo At(string value)
        {
            return new PathInfo(Path.Combine(Scheme, Value, value));
        }

        public PathInfo At(params string[] values)
        {
            string[] joinedValues = new string[values.Length + 2];
            joinedValues[0] = Scheme;
            joinedValues[1] = OriginalString;
            values.CopyTo(joinedValues, 1);

            return new PathInfo(Path.Combine(joinedValues));
        }

        public PathInfo At(PathInfo path)
        {
            return new PathInfo(Path.Combine(Scheme, Value, path.ToString()));
        }

        public PathInfo GetDirectory()
        {
            return new PathInfo(Path.GetDirectoryName(OriginalString));
        }

        public PathInfo CreateDirectory()
        {
            Directory.CreateDirectory(Value);
            return this;
        }

        public string GetFileName()
        {
            return Path.GetFileName(OriginalString);
        }

        public string GetFileNameWithoutExtension()
        {
            return Path.GetFileNameWithoutExtension(OriginalString);
        }

        public string GetExtension()
        {
            return Path.GetExtension(OriginalString);
        }

        public string GetDirectoryName()
        {
            return Path.GetDirectoryName(OriginalString);
        }

        public bool IsFile()
        {
            return !string.IsNullOrEmpty(Path.GetFileName(OriginalString));
        }

        public bool IsDirectory()
        {
            return !IsFile();
        }

        public bool Exists()
        {
            return (IsFile() && FileExists()) || (IsDirectory() && DirectoryExists());
        }

        public bool FileExists()
        {
            return IsFile() && File.Exists(OriginalString);
        }

        public bool DirectoryExists()
        {
            return IsDirectory() && Directory.Exists(OriginalString);
        }

        public bool TryOpenInDefaultApp()
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = OriginalString,
                UseShellExecute = true,
                Verb = "open"
            };

            return Debugger.SafeInvoke(
                () => Process.Start(processStartInfo)
            );
        }

        public override string ToString() => OriginalString;
    }
}
