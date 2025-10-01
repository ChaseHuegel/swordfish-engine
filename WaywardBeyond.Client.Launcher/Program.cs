using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace WaywardBeyond.Client.Launcher;

internal static class Program
{
    private static readonly string _libDir;
    
    static Program()
    {
        string baseDir = AppContext.BaseDirectory;
        _libDir = Path.Combine(baseDir, "lib");
    }
    
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver;
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

        var application = new Application(args);
        return application.Run();
    }

    private static Assembly? AssemblyResolver(object? sender, ResolveEventArgs eventArgs)
    {
        try
        {
            string? name = new AssemblyName(eventArgs.Name).Name;
            string candidate = Path.Combine(_libDir, name + ".dll");
            if (File.Exists(candidate))
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            }
        }
        catch
        {
            // Allow other resolvers to attempt
        }

        return null;
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string fileName = libraryName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileName += ".dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            fileName = "lib" + libraryName + ".so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            fileName = "lib" + libraryName + ".dylib";
        }

        string candidate = Path.Combine(_libDir, fileName);
        if (!File.Exists(candidate))
        {
            return IntPtr.Zero;
        }

        return NativeLibrary.TryLoad(candidate, out IntPtr handle) ? handle : IntPtr.Zero;
    }
}