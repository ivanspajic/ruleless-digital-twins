namespace Models
{
    public class ReconfigurationAction : Action
    {
        public required ConfigurableParameter ConfigurableParameter { get; init; }

        public required Effect Effect { get; init; }
    }
}
