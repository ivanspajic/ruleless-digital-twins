namespace Logic.Models.MapekModels {
    public class CoordinatorSettings {
        public int MaximumMapekRounds { get; set; }

        public required string Environment{ get; set; }

        public bool StartInReactiveMode { get; set; }

        public bool SaveMapekCycleData { get; set; }

        public bool UseCaseBasedFunctionality { get; set; }

        public int SimulationDurationSeconds { get; set; }

        public int LookAheadMapekCycles { get; set; }

        public double PropertyValueFuzziness { get; set; }

        public bool UseEuclid{ get; set; }
        public int SleepyTimeMilliseconds { get; set; }
    }
}
