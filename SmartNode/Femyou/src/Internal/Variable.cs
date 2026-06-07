using System;
using System.Linq;
using System.Xml.Linq;

namespace Femyou.Internal
{
  public class Variable : IVariable
  {
    public Variable(XElement xElement)
    {
      try
      {
        Name = xElement!.Attribute("name")!.Value;
        Description = xElement.Attribute("description")?.Value;
        ValueReference = uint.Parse(xElement!.Attribute("valueReference")!.Value);
        // Get node of type of argument and potential start value.
        var startElem = xElement!.Elements().ElementAt(0);
        StartValue = (startElem.Name.ToString(), startElem.Attribute("start")?.Value);
      }
      catch (Exception e)
      {
        throw new FmuException("Failed to load variable description", e);
      }
    }

    public Variable(string name, string description, uint valueReference)
    {
      Name = name;
      Description = description;
      ValueReference = valueReference;
    }

    public string Name { get; }
    public string Description { get; }
    public uint ValueReference { get; }
    public (string Type, string Value) StartValue { get; }
    }
}