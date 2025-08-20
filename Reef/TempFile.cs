using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Reef;

internal readonly struct TempFile(string path) : IDisposable
{
    public readonly string Path = path;

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch
        {
            //  Do nothing
        }
    }
    
    public static TempFile CreateFromEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
        {
            throw new InvalidOperationException($"Resource '{resourceName}' not found.");
        }

        string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), resourceName);
        
        //  Early out if it already exists
        if (File.Exists(tempFilePath))
        {
            return new TempFile(tempFilePath);
        }
        
        //  Copy out the resource to a temp file
        using (FileStream fileStream = File.Create(tempFilePath))
        {
            resourceStream.CopyTo(fileStream);
        }

        //  On non-windows platforms, ensure the user has ownership over the file
        if (!OperatingSystem.IsWindows())
        {
            var ownFileProcessStartInfo = new ProcessStartInfo("chmod", $"+x \"{tempFilePath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(ownFileProcessStartInfo)?.WaitForExit();
        }

        return new TempFile(tempFilePath);
    }
}