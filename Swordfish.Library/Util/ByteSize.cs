using System;
using System.Text;

namespace Swordfish.Library.Util
{
    public class ByteSize
    {
        public const long KB = 1024;
        public const long MB = KB * 1024;
        public const long GB = MB * 1024;

        private const long HalfKB = KB / 2;
        private const long HalfMB = MB / 2;
        private const long HalfGB = GB / 2;

        public readonly long Bytes;
        public double Kilobytes => Bytes / (double)KB;
        public double Megabytes => Bytes / (double)MB;
        public double Gigabytes => Bytes / (double)GB;

        public ByteSize(long bytes)
        {
            Bytes = bytes;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            if (Bytes < HalfKB)
                return $"{Bytes:0} bytes";

            if (Bytes < HalfMB)
                return $"{Kilobytes:0.00} KB";

            if (Bytes < HalfGB)
                return $"{Megabytes:0.00} MB";

            return $"{Gigabytes:0.00} GB";
        }

        public static ByteSize FromBytes(long bytes) => new ByteSize(bytes);

        public static ByteSize FromKilobytes(long kilobytes) => new ByteSize(kilobytes * KB);

        public static ByteSize FromMegabytes(long megabytes) => new ByteSize(megabytes * MB);

        public static ByteSize FromGigabytes(long gigabytes) => new ByteSize(gigabytes * GB);
    }
}
