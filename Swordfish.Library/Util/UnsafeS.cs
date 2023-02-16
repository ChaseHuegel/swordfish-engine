using System.Text;

namespace Swordfish.Library.Util
{
    public static unsafe class UnsafeS
    {
        public static string ToString(byte* ptr)
        {
            StringBuilder stringBuilder = new StringBuilder();

            int offset = 0;
            while (ptr[offset] != '\0')
            {
                stringBuilder.Append((char)ptr[offset]);
                offset++;
            }

            return stringBuilder.ToString();
        }

        public static string ToString(char* ptr)
        {
            StringBuilder stringBuilder = new StringBuilder();

            int offset = 0;
            while (ptr[offset] != '\0')
            {
                stringBuilder.Append(ptr[offset]);
                offset++;
            }

            return stringBuilder.ToString();
        }
    }
}
