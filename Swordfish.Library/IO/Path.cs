using System.Diagnostics;
using System.IO;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

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
