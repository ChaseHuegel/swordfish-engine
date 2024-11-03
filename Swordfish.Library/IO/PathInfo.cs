using System;
using System.IO;

namespace Swordfish.Library.IO;

public readonly struct PathInfo : IEquatable<PathInfo>
{
    public readonly string OriginalString;

    public readonly string Value;

    public readonly string Scheme;

    public PathInfo(string scheme, string value)
    {
        Scheme = scheme;
        Value = value;
        OriginalString = value;
    }

    public PathInfo(string value)
    {
        OriginalString = value;

        int schemeEndIndex = value.IndexOf("://", StringComparison.Ordinal);
        Value = schemeEndIndex > 0 ? value[(schemeEndIndex + 3)..] : value;
        Scheme = schemeEndIndex > 0 ? value[..schemeEndIndex].ToLowerInvariant() : "file";
    }
    
    public static implicit operator PathInfo(string value) => new(value);
    public static implicit operator string(PathInfo path) => path.Value;

    public PathInfo At(string value)
    {
        return new PathInfo(Scheme, Path.Combine(Value, value));
    }

    public PathInfo At(params string[] values)
    {
        var joinedValues = new string[values.Length + 1];
        joinedValues[0] = Value;
        values.CopyTo(joinedValues, 1);

        return new PathInfo(Scheme, Path.Combine(joinedValues));
    }

    public PathInfo At(PathInfo path)
    {
        return new PathInfo(Scheme, Path.Combine(Value, path.Value));
    }

    public PathInfo Normalize()
    {
        return new PathInfo(Scheme, Value.Replace(@"\\", @"\").Replace('\\', '/'));
    }

    public PathInfo GetDirectory()
    {
        return new PathInfo(Path.GetDirectoryName(Value) ?? string.Empty);
    }

    public PathInfo CreateDirectory()
    {
        Directory.CreateDirectory(Value);
        return this;
    }

    public string GetFileName()
    {
        return Path.GetFileName(Value);
    }

    public string GetFileNameWithoutExtension()
    {
        return Path.GetFileNameWithoutExtension(Value);
    }

    public string GetExtension()
    {
        return Path.GetExtension(Value);
    }

    public string GetDirectoryName()
    {
        return Path.GetDirectoryName(Value);
    }

    public bool IsFile()
    {
        return !string.IsNullOrEmpty(Path.GetFileName(Value));
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
        return IsFile() && File.Exists(Value);
    }

    public bool DirectoryExists()
    {
        return Directory.Exists(Value);
    }

    public override string ToString()
    {
        return OriginalString;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Scheme.GetHashCode(), Value.GetHashCode());
    }

    public bool Equals(PathInfo other)
    {
        return OriginalString == other.OriginalString && Value == other.Value && Scheme == other.Scheme;
    }

    public override bool Equals(object obj)
    {
        return obj is PathInfo other && Equals(other);
    }
}