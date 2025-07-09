namespace Logic.Models.OntologicalModels
{
    internal class Property : NamedIndividual
    {
        public required object Value { get; set; }

        public required string OwlType { get; init; }
    }
}
