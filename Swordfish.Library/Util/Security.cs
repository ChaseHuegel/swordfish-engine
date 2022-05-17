using System.Security.Cryptography;

namespace Swordfish.Library.Util
{
    public static class Security
    {
        public static byte[] Salt(int length)
        {
            byte[] buffer = new byte[length];
            RandomNumberGenerator.Create().GetBytes(buffer);
            return buffer;
        }

        public static byte[] Hash(byte[] value)
        {
            return SHA256.Create().ComputeHash(value);
        }

        public static byte[] SaltedHash(byte[] value, int saltLength)
        {
            return SaltedHash(value, Salt(saltLength));
        }

        public static byte[] SaltedHash(byte[] value, byte[] salt)
        {
            byte[] salted = new byte[value.Length + salt.Length];

            for (int i = 0; i < value.Length; i++)
                salted[i] = value[i];

            for (int i = 0; i < salt.Length; i++)
                salted[value.Length + i] = salt[i];

            return Hash(salted);
        }
    }
}
