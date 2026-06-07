using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using static Femyou.IModel;

namespace Femyou.Internal
{
  public interface IModelVersion
  {
    string CoSimulationElementName { get; }
    string GuidAttributeName { get; }
    string RelativePath(string name, Architecture architecture, PlatformID platform);
    Library Load(string path);
    Library Load(string path, Collection<UnsupportedFunctions> unsupportedFunctions);
  }
}