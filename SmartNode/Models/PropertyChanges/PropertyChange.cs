using Models.Properties;

namespace Models.PropertyChanges
{
    public class PropertyChange
    {
        public required string Identifier { get; init; }

        public required Property TargetProperty { get; init; }

        public Effect AffectedWithEffect { get; init; }
    }
}
