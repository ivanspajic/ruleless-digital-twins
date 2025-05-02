using Models.Properties;

namespace Models
{
    public class OptimalCondition
    {
        public required Property Property { get; init; }

        public required Func<Property, bool> IsConditionMet { get; init; }
    }
}
