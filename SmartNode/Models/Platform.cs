using Models.Properties;

namespace Models
{
    public class Platform
    {
        public required IReadOnlyCollection<Devices.System> Devices { get; init; }

        public required Property OptimizedProperty { get; init; }
    }
}
