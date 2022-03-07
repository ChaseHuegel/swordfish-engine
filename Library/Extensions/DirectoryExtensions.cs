using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Swordfish.Library.Extensions
{
    public static class DirectoryExtensions
    {
        public static FileInfo[] GetFiles(this DirectoryInfo directoryInfo, params string[] extensions)
        {
            HashSet<string> allowedExtensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

            return directoryInfo.EnumerateFiles().Where(file => allowedExtensions.Contains(file.Extension)).ToArray();
        }
    }
}
