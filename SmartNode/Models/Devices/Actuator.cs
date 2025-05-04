namespace Models.Devices
{
    public class Actuator
    {
        public required string Name { get; init; }

        public int StateDomain { get; init; }
    }
}
