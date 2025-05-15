namespace Models
{
    public class ObservableProperty : NamedIndividual
    {
        // ObservableProperties will only have estimated values within some range, as dictated by the devices that
        // measure it. For example, a room temperature could be measured by two sensors, each reporting a slightly
        // different value. In our ontology (based on SOSA/SSN), these measured property values are Outputs of
        // Procedures implemented by Sensors. As a result, the original observed room temperature property would
        // have a possible value range between a minimum and a maximum, as dictated by the two slightly different
        // sensor measurements.
        public required object LowerLimitValue { get; init; }

        public required object UpperLimitValue { get; init; }

        public required string OwlType { get; set; }
    }
}
