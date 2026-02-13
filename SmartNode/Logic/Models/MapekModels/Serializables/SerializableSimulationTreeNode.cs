namespace Logic.Models.MapekModels.Serializables {
    public class SerializableSimulationTreeNode {
        public required SerializableSimulation NodeItem { get; set; }

        public required IEnumerable<SerializableSimulationTreeNode> Children { get; set; }
    }
}
