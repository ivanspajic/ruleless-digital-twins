using Models.Properties;

namespace Models
{
    public class Platform
    {
        public required IReadOnlyCollection<Devices.System> HostedDevices { get; init; }

        public required Property OptimizationTarget { get; init; }
    }
}
