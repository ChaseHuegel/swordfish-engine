using Swordfish.Library.Types;

namespace Swordfish.Library.Util
{
    public static class ByteConverter
    {
        public static MultiBool ToMultiBool(byte[] value, int startIndex)
        {
            return (MultiBool) value[startIndex];
        }

        public static byte[] GetBytes(MultiBool value)
        {
            return new byte[1] { value };
        }
    }
}
