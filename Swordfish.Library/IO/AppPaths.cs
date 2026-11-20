using System;
using System.IO;
using System.Reflection;

namespace Swordfish.Library.IO;

/// <summary>Resolves cross-platform application data, config, and log directories.</summary>
public readonly struct AppPaths
{
    private const string DEFAULT_APP_NAME = "Swordfish";
    private const string CONFIG_DIRECTORY = "config";
    private const string LOGS_DIRECTORY = "logs";
    private const string CRASHES_DIRECTORY = "crashes";

    /// <summary>Root directory for application data such as saves and user overrides.</summary>
    public PathInfo DataRoot { get; }

    /// <summary>Directory where user-mutable config files are saved.</summary>
    public PathInfo ConfigRoot => DataRoot.At(CONFIG_DIRECTORY);

    /// <summary>Directory where log files are written.</summary>
    public PathInfo LogsRoot => DataRoot.At(LOGS_DIRECTORY);

    /// <summary>Directory where crash reports and crash dumps are written.</summary>
    public PathInfo CrashesRoot => DataRoot.At(CRASHES_DIRECTORY);

    /// <summary>Whether <see cref="DataRoot"/> should be mounted into the virtual file system as a user override.</summary>
    public bool MountInVfs { get; }

    private AppPaths(PathInfo dataRoot, bool mountInVfs)
    {
        DataRoot = dataRoot;
        MountInVfs = mountInVfs;
    }

    /// <summary>Resolves paths under the local user's OS application-data directory, or the application base directory when none resolves.</summary>
    /// <param name="appName">Application folder name. Defaults to the entry assembly name.</param>
    /// <param name="companyName">Optional company folder placed above the application folder.</param>
    public static AppPaths ForLocalUser(string appName = null, string companyName = null)
    {
        string resolvedName = string.IsNullOrWhiteSpace(appName)
            ? Assembly.GetEntryAssembly()?.GetName().Name
            : appName;

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = DEFAULT_APP_NAME;
        }

        //  DoNotVerify keeps the XDG directory usable before it exists.
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return new AppPaths(new PathInfo(AppContext.BaseDirectory), mountInVfs: false);
        }

        string root = string.IsNullOrWhiteSpace(companyName)
            ? Path.Combine(localAppData, resolvedName)
            : Path.Combine(localAppData, companyName, resolvedName);

        return new AppPaths(new PathInfo(root), mountInVfs: true);
    }

    /// <summary>Resolves paths under the current working directory without mounting it in VFS.</summary>
    public static AppPaths ForWorkingDirectory()
    {
        return new AppPaths(new PathInfo(Environment.CurrentDirectory), mountInVfs: false);
    }

    /// <summary>Resolves paths under an explicit directory.</summary>
    public static AppPaths ForDirectory(PathInfo dataRoot, bool mountInVfs = false)
    {
        return new AppPaths(dataRoot, mountInVfs);
    }

    /// <summary>Resolves a relative path against <see cref="DataRoot"/>, or returns a rooted path unchanged.</summary>
    public PathInfo ResolveDataPath(PathInfo path)
    {
        return Path.IsPathRooted(path.Value) ? path : DataRoot.At(path);
    }
}
