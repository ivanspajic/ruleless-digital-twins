using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static Femyou.IModel;

namespace Femyou.Internal
{
  public class ModelVersion2 : IModelVersion
  {
    public string CoSimulationElementName { get; } = "CoSimulation";
    public string GuidAttributeName { get; } = "guid";
    public string RelativePath(string name, Architecture architecture, PlatformID platform) =>
      platform switch
      {
        PlatformID.Unix => Path.Combine("binaries", "linux" + MapArchitecture(architecture), name + ".so"),
        PlatformID.Win32NT => Path.Combine("binaries", "win" + MapArchitecture(architecture), name + ".dll"),
        _ => throw new FmuException($"Unsupported operating system {platform}"),
      };

    public Library Load(string path) { return Load(path, new Collection<UnsupportedFunctions>([])); }
    public Library Load(string path, Collection<UnsupportedFunctions> unsupportedFunctions)
    {
      // We're adding a bit more context here to distinguish having an .so for the wrong arch.
      Debug.Assert(File.Exists(path), $"File does not exist: {path}");
      try
      {
        return new Library2(path, unsupportedFunctions);
      }
      catch (FileNotFoundException e)
      {
        throw new FmuException("File exists but couldn't load native library", e);
      }
    }

    private string MapArchitecture(Architecture architecture) =>
      architecture switch
      {
        Architecture.X86 => "32",
        Architecture.X64 => "64",
        Architecture.Arm64 => "64",
        _ => throw new FmuException($"Unsupported architecture {architecture}"),
      };
  }
}