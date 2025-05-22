namespace Models
{
    public class ActuatorState
    {
        public required string Actuator { get; init; }

        // Temporarily a string until we get to handling this.
        public required string StateValueRange { get; init; }
    }
}
