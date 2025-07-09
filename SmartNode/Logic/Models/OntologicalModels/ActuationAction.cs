namespace Logic.Models.OntologicalModels
{
    internal class ActuationAction : Action
    {
        public required ActuatorState ActuatorState { get; init; }

        public required string ActedOnProperty { get; init; }
    }
}
