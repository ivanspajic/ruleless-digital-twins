namespace Femyou
{
  public interface IVariable
  {
    string Name { get; }
    string Description { get; }
    public (string Type, string Value) StartValue { get; }
  }
}