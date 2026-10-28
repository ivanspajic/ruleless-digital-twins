using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Storage;

namespace TestProject.Utilities {
    internal static class FusekiTestHelper {
        // Replaces the knowledge base in the local Fuseki server (see the devcontainer) with the given model file.
        public static void UploadInstanceModel(string modelFilepath) {
            var model = new Graph();
            new TurtleParser().Load(model, modelFilepath);
            // A base IRI would make the connector store the model in a named graph instead of the default graph.
            model.BaseUri = null;
            new FusekiConnector(new Uri("http://localhost:3030/ds/data")).SaveGraph(model);
        }
    }
}
