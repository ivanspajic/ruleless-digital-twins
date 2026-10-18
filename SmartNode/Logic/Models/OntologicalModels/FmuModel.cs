namespace Logic.Models.OntologicalModels
{
    public class FmuModel : NamedIndividual
    {
        public required string Filepath { get; init; }

        public double SimulationFidelitySeconds { get; init; }
    }
}
