namespace Logic.Models.OntologicalModels
{
    public class Actuator : NamedIndividual
    {
        public string? Type { get; internal set; } // TODO: review if this should be nullable.
        public string? ParameterName { get; internal set; } // XXX: Hotfix to resolve naming issues (#41).
    }
}
