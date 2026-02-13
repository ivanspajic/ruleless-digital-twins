namespace Logic.Models.MapekModels.Serializables {
    public class SerializableSimulation {
        public int Index { get; init; }

        public required IEnumerable<SerializableAction> Actions { get; init; }

        public required SerializablePropertyCache PropertyCache { get; init; }
    }
}
