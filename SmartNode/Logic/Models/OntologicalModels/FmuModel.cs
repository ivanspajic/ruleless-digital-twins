namespace Logic.Models.OntologicalModels
{
    public class FmuModel : NamedIndividual
    {
        public required string FilePath { get; init; }

        public int SimulationFidelitySeconds { get; init; }
    }
}
