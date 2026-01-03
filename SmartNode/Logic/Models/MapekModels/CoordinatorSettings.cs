namespace Logic.Models.MapekModels {
    public class CoordinatorSettings {
        public int MaximumMapekRounds { get; set; }

        public bool UseSimulatedEnvironment { get; set; }

        public int SimulationTimeSeconds { get; set; }

        public int LookAheadMapekCycles { get; set; }

        public bool ReactiveMode { get; set; }
    }
}
