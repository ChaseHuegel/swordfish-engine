using System.Security.Cryptography;
using System.Text;
using Swordfish.Library.Util;

namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Computes SHA-256 digests for file content and canonical path keys.
/// </summary>
public static class ContentHash
{
    /// <summary>
    ///     Computes a lowercase hexadecimal SHA-256 digest of <paramref name="bytes"/>.
    /// </summary>
    public static string ComputeSha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytes, digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>
    ///     Computes a lowercase hexadecimal SHA-256 digest of UTF-8 encoded <paramref name="text"/>.
    /// </summary>
    public static string ComputeSha256Hex(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return ComputeSha256Hex(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    ///     Computes the SHA-256 digest of a file on disk, returning null when the file does not exist.
    /// </summary>
    public static Result<string?> ComputeFileSha256Hex(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<string?>.FromFailure("File path is required.");
        }

        if (!File.Exists(filePath))
        {
            return Result<string?>.FromSuccess(null);
        }

        try
        {
            using FileStream stream = File.OpenRead(filePath);
            byte[] digest = SHA256.HashData(stream);
            return Result<string?>.FromSuccess(Convert.ToHexStringLower(digest));
        }
        catch (Exception ex)
        {
            return Result<string?>.FromFailure(ex);
        }
    }
}
