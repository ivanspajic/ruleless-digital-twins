using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using MongoDB.Bson.Serialization.Attributes;

namespace Logic.Models.DatabaseModels {
    public class Case {
        [BsonId]
        public string? ID { get; set; }

        public required IEnumerable<Property> QuantizedProperties { get; set; }

        public required IEnumerable<Condition> QuantizedOptimalConditions { get; set; }

        public int LookAheadCycles { get; set; }

        public int SimulationDurationSeconds { get; set; }

        public int Index { get; set; }

        public required Simulation Simulation { get; set; }
    }
}
