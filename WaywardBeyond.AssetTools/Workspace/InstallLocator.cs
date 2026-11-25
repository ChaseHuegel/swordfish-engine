using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Discovers Wayward Beyond installations across Windows registry, Steam libraries, and Linux Proton prefixes.
/// </summary>
public sealed partial class InstallLocator
{
    private const string GAME_FOLDER_NAME = "Wayward Beyond";
    private const uint KEY_READ = 0x20019;
    private const uint REG_SZ = 1;
    private static readonly nint HKEY_CURRENT_USER = unchecked((nint)0x80000001);
    private static readonly nint HKEY_LOCAL_MACHINE = unchecked((nint)0x80000002);

    /// <summary>
    ///     Locates all candidate Wayward Beyond installations without guessing when more than one exists.
    /// </summary>
    public InstallLocateResult Locate(InstallSearchRoots? customRoots = null)
    {
        List<InstallCandidate> found = [];
        HashSet<string> seen = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        InstallSearchRoots roots = customRoots ?? BuildDefaultRoots();

        if (roots.ProbeWindowsRegistry && OperatingSystem.IsWindows())
        {
            ProbeWindowsRegistryInstalls(found, seen);
        }

        for (int i = 0; i < roots.DirectInstallCandidates.Count; i++)
        {
            TryAddInstallCandidate(roots.DirectInstallCandidates[i], "Direct", found, seen);
        }

        for (int i = 0; i < roots.SteamRoots.Count; i++)
        {
            ScanSteamRoot(roots.SteamRoots[i], found, seen);
        }

        found.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.InstallPath, right.InstallPath));
        return new InstallLocateResult(found);
    }

    /// <summary>
    ///     Parses Steam <c>libraryfolders.vdf</c> content and returns every declared library path.
    /// </summary>
    public static IReadOnlyList<string> ParseSteamLibraryFoldersVdf(string vdfContent)
    {
        if (string.IsNullOrWhiteSpace(vdfContent))
        {
            return [];
        }

        List<string> libraries = [];
        MatchCollection matches = LibraryPathRegex().Matches(vdfContent);
        for (int i = 0; i < matches.Count; i++)
        {
            string raw = matches[i].Groups[1].Value;
            string unescaped = raw.Replace("\\\\", "\\", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(unescaped))
            {
                libraries.Add(unescaped);
            }
        }

        return libraries;
    }

    private static InstallSearchRoots BuildDefaultRoots()
    {
        List<string> steamRoots = [];
        if (OperatingSystem.IsLinux())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                steamRoots.Add(Path.Combine(home, ".local", "share", "Steam"));
                steamRoots.Add(Path.Combine(home, ".steam", "steam"));
                steamRoots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                steamRoots.Add(Path.Combine(programFilesX86, "Steam"));
            }
        }

        return new InstallSearchRoots(steamRoots, [], ProbeWindowsRegistry: true);
    }

    private static void ScanSteamRoot(string steamRoot, List<InstallCandidate> found, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(steamRoot) || !Directory.Exists(steamRoot))
        {
            return;
        }

        List<string> libraryRoots = [steamRoot];
        string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                IReadOnlyList<string> extraLibraries = ParseSteamLibraryFoldersVdf(File.ReadAllText(vdfPath));
                for (int i = 0; i < extraLibraries.Count; i++)
                {
                    libraryRoots.Add(extraLibraries[i]);
                }
            }
            catch (IOException ex)
            {
                Trace.TraceWarning($"Failed reading Steam library file '{vdfPath}': {ex.Message}");
            }
        }

        for (int i = 0; i < libraryRoots.Count; i++)
        {
            string steamApps = Path.Combine(libraryRoots[i], "steamapps");
            if (!Directory.Exists(steamApps))
            {
                continue;
            }

            string commonCandidate = Path.Combine(steamApps, "common", GAME_FOLDER_NAME);
            TryAddInstallCandidate(commonCandidate, "SteamLibrary", found, seen);

            ScanProtonCompatdata(Path.Combine(steamApps, "compatdata"), found, seen);
        }
    }

    private static void ScanProtonCompatdata(string compatdataDir, List<InstallCandidate> found, HashSet<string> seen)
    {
        if (!Directory.Exists(compatdataDir))
        {
            return;
        }

        string[] appDirs;
        try
        {
            appDirs = Directory.GetDirectories(compatdataDir);
        }
        catch (IOException ex)
        {
            Trace.TraceWarning($"Failed enumerating Proton compatdata '{compatdataDir}': {ex.Message}");
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.TraceWarning($"Access denied enumerating Proton compatdata '{compatdataDir}': {ex.Message}");
            return;
        }

        for (int i = 0; i < appDirs.Length; i++)
        {
            string driveC = Path.Combine(appDirs[i], "pfx", "drive_c");
            if (!Directory.Exists(driveC))
            {
                continue;
            }

            string steamInPrefix = Path.Combine(driveC, "Program Files (x86)", "Steam", "steamapps", "common", GAME_FOLDER_NAME);
            TryAddInstallCandidate(steamInPrefix, "ProtonCompatdata", found, seen);

            string standaloneInPrefix = Path.Combine(driveC, "Program Files", GAME_FOLDER_NAME);
            TryAddInstallCandidate(standaloneInPrefix, "ProtonCompatdata", found, seen);
        }
    }

    private static void ProbeWindowsRegistryInstalls(List<InstallCandidate> found, HashSet<string> seen)
    {
        string? steamFromHkcu = ReadWindowsRegistryString(HKEY_CURRENT_USER, @"Software\Valve\Steam", "SteamPath");
        if (!string.IsNullOrWhiteSpace(steamFromHkcu))
        {
            ScanSteamRoot(steamFromHkcu, found, seen);
        }

        string? steamFromHklm = ReadWindowsRegistryString(HKEY_LOCAL_MACHINE, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
                                ?? ReadWindowsRegistryString(HKEY_LOCAL_MACHINE, @"SOFTWARE\Valve\Steam", "InstallPath");
        if (!string.IsNullOrWhiteSpace(steamFromHklm))
        {
            ScanSteamRoot(steamFromHklm, found, seen);
        }

        string? gameInstall = ReadWindowsRegistryString(HKEY_LOCAL_MACHINE, @"SOFTWARE\Wayward Beyond", "InstallPath")
                              ?? ReadWindowsRegistryString(HKEY_CURRENT_USER, @"Software\Wayward Beyond", "InstallPath");
        if (!string.IsNullOrWhiteSpace(gameInstall))
        {
            TryAddInstallCandidate(gameInstall, "WindowsRegistry", found, seen);
        }
    }

    private static void TryAddInstallCandidate(
        string candidatePath,
        string source,
        List<InstallCandidate> found,
        HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || !Directory.Exists(candidatePath))
        {
            return;
        }

        string fullPath = Path.GetFullPath(candidatePath);
        if (!LooksLikeGameInstall(fullPath) || !seen.Add(fullPath))
        {
            return;
        }

        bool hasModulesConfig = File.Exists(Path.Combine(fullPath, "config", "modules.toml"));
        found.Add(new InstallCandidate(fullPath, source, hasModulesConfig));
    }

    private static bool LooksLikeGameInstall(string directory)
    {
        return Directory.Exists(Path.Combine(directory, "modules")) ||
               File.Exists(Path.Combine(directory, "config", "modules.toml")) ||
               File.Exists(Path.Combine(directory, "WaywardBeyond.Client.Launcher.exe")) ||
               File.Exists(Path.Combine(directory, "WaywardBeyond.Client.Launcher"));
    }

    private static string? ReadWindowsRegistryString(nint hive, string subKey, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        if (RegOpenKeyExW(hive, subKey, 0, KEY_READ, out nint keyHandle) != 0)
        {
            return null;
        }

        try
        {
            uint dataSize = 0;
            if (RegQueryValueExW(keyHandle, valueName, 0, out uint type, null, ref dataSize) != 0 ||
                type != REG_SZ ||
                dataSize == 0)
            {
                return null;
            }

            var buffer = new char[dataSize / sizeof(char)];
            if (RegQueryValueExW(keyHandle, valueName, 0, out _, buffer, ref dataSize) != 0)
            {
                return null;
            }

            return new string(buffer).TrimEnd('\0');
        }
        finally
        {
            RegCloseKey(keyHandle);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegOpenKeyExW(nint hKey, string lpSubKey, uint ulOptions, uint samDesired, out nint phkResult);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegQueryValueExW(
        nint hKey,
        string lpValueName,
        nint lpReserved,
        out uint lpType,
        char[]? lpData,
        ref uint lpcbData);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(nint hKey);

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}
