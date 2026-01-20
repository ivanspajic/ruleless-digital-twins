using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels {
    public class Cache {
        public required PropertyCache PropertyCache { get; set; }

        public required IEnumerable<SoftSensorTreeNode> SoftSensorTreeNodes { get; set; }

        public required IEnumerable<Condition> OptimalConditions { get; set; }
    }
}
