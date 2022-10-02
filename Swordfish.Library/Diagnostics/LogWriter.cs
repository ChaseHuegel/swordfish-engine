using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Swordfish.Library.Diagnostics
{
    public class LogWriter : TextWriter
    {
        public EventHandler<string> LineAdded;

        private List<string> lines = new List<string>();

        private TextWriter original;

        public LogWriter() { }

        public LogWriter(TextWriter original)
        {
            this.original = original;
        }

        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }

        public override void WriteLine(string value)
        {
            if (value == null) return;

            lines.Add(value);
            original?.WriteLine(value);

            LineAdded?.Invoke(this, value);
        }

        public List<string> GetLines() => lines;
        public List<string> GetLines(int count) => lines.GetRange(Math.Max(lines.Count - count - 1, 0), lines.Count - Math.Max(lines.Count - count - 1, 0));
    }
}