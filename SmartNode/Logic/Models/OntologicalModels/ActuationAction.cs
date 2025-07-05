namespace Logic.Models.OntologicalModels
{
    public class ActuationAction : Action
    {
        public required ActuatorState ActuatorState { get; init; }

        public required string ActedOnProperty { get; init; }
    }
}
