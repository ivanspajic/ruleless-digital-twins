using VDS.RDF.Storage;

namespace Logic.Models.MapekModels {
    public class FusekiArguments {
        // Graph Store endpoint of the dataset holding the instance model.
        public readonly FusekiConnector InstanceModelUri = new(new Uri("http://localhost:3030/ds/data"));

        // Graph Store endpoint of the service that applies the inference rules to the model put into it.
        public readonly FusekiConnector InferredModelUri = new(new Uri("http://localhost:3030/inf/data"));
    }
}
