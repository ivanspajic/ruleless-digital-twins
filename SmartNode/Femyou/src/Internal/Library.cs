using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NativeLibraryLoader;

namespace Femyou.Internal
{
  public abstract class Library : IDisposable
  {
    public Library(string path)
    {
      AddNativeDependencyDirectory(path);
      FmuLibrary = new NativeLibrary(path);
    }

    private static readonly object PathLock = new();

    private static void AddNativeDependencyDirectory(string libraryPath)
    {
      if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        return;

      var directory = Path.GetDirectoryName(libraryPath);
      if (string.IsNullOrWhiteSpace(directory))
        return;

      var normalizedDirectory = NormalizePath(directory);
      lock (PathLock)
      {
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathEntries = currentPath
          .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
          .Select(NormalizePath);

        if (pathEntries.Contains(normalizedDirectory, StringComparer.OrdinalIgnoreCase))
          return;

        Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + currentPath);
      }
    }

    private static string NormalizePath(string path) =>
      Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    protected readonly NativeLibrary FmuLibrary;
    public void Dispose()
    {
      FmuLibrary.Dispose();
    }
    public abstract Callbacks CreateCallbacks(Instance instance, ICallbacks cb);

    public abstract IntPtr Instantiate(string name, string modelGuid, string modelTmpFolder, Callbacks callbacks);
    public abstract void Setup(IntPtr handle, double currentTime);
    public abstract void Setup(IntPtr handle, double currentTime, Func<bool> initialization);
    public abstract void Step(IntPtr handle, double currentTime, double step);
    public abstract void Shutdown(IntPtr handle, bool started);
    public abstract void Reset(IntPtr handle);
    public abstract void SetTime(IntPtr handle, System.Double time);
    
    public abstract IEnumerable<double> ReadReal(IntPtr handle, IEnumerable<IVariable> variables);
    public abstract IEnumerable<int> ReadInteger(IntPtr handle, IEnumerable<IVariable> variables);
    public abstract IEnumerable<bool> ReadBoolean(IntPtr handle, IEnumerable<IVariable> variables);
    public abstract IEnumerable<string> ReadString(IntPtr handle, IEnumerable<IVariable> variables);
    public abstract void WriteReal(IntPtr handle, IEnumerable<(IVariable, double)> variables);
    public abstract void WriteInteger(IntPtr handle, IEnumerable<(IVariable, int)> variables);
    public abstract void WriteBoolean(IntPtr handle, IEnumerable<(IVariable, bool)> variables);
    public abstract void WriteString(IntPtr handle, IEnumerable<(IVariable, string)> variables);

  }
}
