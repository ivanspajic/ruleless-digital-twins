namespace Logic.Models.OntologicalModels
{
    internal class ReconfigurationAction : Action
    {
        public required ConfigurableParameter ConfigurableParameter { get; init; }

        public required Effect Effect { get; init; }

        // This isn't required on instantiation as the reconfiguration value isn't known until
        // after the simulations have been completed.
        public object AltersBy { get; set; }
    }
}
