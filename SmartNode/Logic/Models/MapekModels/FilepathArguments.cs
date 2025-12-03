namespace Logic.Models.MapekModels {
    public class FilepathArguments {
        public required string InferenceEngineFilepath { get; set; }

        public required string OntologyFilepath { get; set; }

        public required string InstanceModelFilepath { get; set; }

        public required string InferenceRulesFilepath { get; set; }

        public required string InferredModelFilepath { get; set; }

        public required string FmuDirectory { get; set; }

        public required string DataDirectory { get; set; }
    }
}
