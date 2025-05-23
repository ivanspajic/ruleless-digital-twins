namespace Models
{
    public class InputOutput : NamedIndividual
    {
        public required object Value { get; init; }

        public required string OwlType { get; init; }
    }
}
