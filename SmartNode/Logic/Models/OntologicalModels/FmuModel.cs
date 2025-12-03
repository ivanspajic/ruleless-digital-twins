namespace Logic.Models.OntologicalModels
{
    public class FmuModel : NamedIndividual
    {
        public required string Filepath { get; init; }

        public int SimulationFidelitySeconds { get; init; }
    }
}
