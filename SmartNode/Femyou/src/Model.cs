using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using Femyou.Internal;
using static Femyou.IModel;

namespace Femyou
{
  public class Model
  {
    public static IModel Load(string fmuPath)
    {
      return Load(fmuPath, new Collection<UnsupportedFunctions>((UnsupportedFunctions[])Enum.GetValues(typeof(UnsupportedFunctions))));
    }
    public static IModel Load(string fmuPath, Collection<UnsupportedFunctions> unsupportedFunctions)
    {
      var TmpFolder = CreateTmpFolder(fmuPath);
      ZipFile.ExtractToDirectory(fmuPath, TmpFolder, true);
      return new ModelImpl(TmpFolder, unsupportedFunctions);
    }

    public static IModel Load(Stream fmuStream, string fmuPath)
    {
      var TmpFolder = CreateTmpFolder(fmuPath);
      ZipFile.ExtractToDirectory(fmuStream, TmpFolder, true);
      return new ModelImpl(TmpFolder, new Collection<UnsupportedFunctions>((UnsupportedFunctions[])Enum.GetValues(typeof(UnsupportedFunctions))));
    }

    private static string CreateTmpFolder(string fmuPath) =>
      Path.Combine(Path.GetTempPath(), nameof(Femyou), Path.GetFileName(fmuPath) + "-" + Guid.NewGuid().ToString("N"));
  }
}
