using System.Security.Cryptography;

// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Security;

// ReSharper disable once UnusedType.Global
public static class Hashing
{
    public static byte[] Salt(int length)
    {
        var buffer = new byte[length];
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
        var salted = new byte[value.Length + salt.Length];

        for (var i = 0; i < value.Length; i++)
        {
            salted[i] = value[i];
        }

        for (var i = 0; i < salt.Length; i++)
        {
            salted[value.Length + i] = salt[i];
        }

        return Hash(salted);
    }
}