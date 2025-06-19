namespace Models
{
    public class ActuationAction : Action
    {
        public required ActuatorState ActuatorState { get; init; }

        // This isn't necessary, but it reduces re-querying for relevant Properties.
        public required string ActedOnProperty { get; init; }
    }
}
