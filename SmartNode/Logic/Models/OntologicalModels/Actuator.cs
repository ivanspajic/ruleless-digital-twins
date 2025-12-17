namespace Logic.Models.OntologicalModels
{
    public class Actuator : NamedIndividual
    {
        public string? Type { get; internal set; } // TODO: review if this should be nullable.
    }
}
