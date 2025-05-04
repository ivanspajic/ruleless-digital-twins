using Models.Properties;
using System.Numerics;

namespace Models.Executions
{
    public class ReconfigurationExecution<T> : Execution where T : INumber<T>
    {
        public required ConfigurableProperty<T> Property { get; init; }

        public Effect AffectedWithEffect { get; init; }
    }
}
