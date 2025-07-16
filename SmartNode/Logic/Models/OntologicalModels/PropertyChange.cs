namespace Logic.Models.OntologicalModels
{
    internal class PropertyChange : NamedIndividual
    {
        public required Property Property { get; init; }

        public required Effect OptimizeFor { get; init; }
    }
}
