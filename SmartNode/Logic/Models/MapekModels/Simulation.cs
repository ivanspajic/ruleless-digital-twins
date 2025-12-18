namespace Logic.Models.MapekModels
{
    public class Simulation
    {
        public Simulation(PropertyCache propertyCache)
        {
            PropertyCache = propertyCache;
            Actions = [];
            Index = -1;
        }

        public int Index { get; init; }

        public IEnumerable<OntologicalModels.Action> Actions { get; init; }

        public PropertyCache? PropertyCache { get; set; }
    }
}
