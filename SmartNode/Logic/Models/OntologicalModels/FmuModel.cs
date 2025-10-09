namespace Logic.Models.OntologicalModels
{
    internal class FmuModel : NamedIndividual
    {
        public required string FilePath { get; init; }

        public int SimulationFidelitySeconds { get; init; }
    }
}
