namespace Logic.Models.OntologicalModels
{
    internal class ReconfigurationAction : Action
    {
        public required ConfigurableParameter ConfigurableParameter { get; init; }

        // This isn't required on instantiation as the reconfiguration value isn't known until
        // the Plan phase has been reached and a simulation granularity has been provided.
        public object NewParameterValue { get; set; }
    }
}
