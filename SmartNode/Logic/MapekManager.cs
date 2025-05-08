using Logic.DeviceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Logic
{
    public class MapekManager : IMapekManager
    {
        private const string DtPrefix = "http://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation:";
        private const string SosaPrefix = "sosa:";
        private const string SsnPrefix = "ssn:";
        private const string RdfPrefix = "rdf:";
        private const string OwlPrefix = "owl:";

        private bool _isLoopActive = false;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MapekManager> _logger;

        public MapekManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger<MapekManager>>()!;
        }

        public void StartLoop(string filePath)
        {
            _isLoopActive = true;

            RunMapekLoop(filePath);
        }

        public void StopLoop()
        {
            _isLoopActive = false;
        }

        private void RunMapekLoop(string filePath)
        {
            _logger.LogInformation("Starting the MAPE-K loop...");

            IGraph graph = InitializeGraph(filePath);
            
            // If nothing was loaded, simply return.
            if (graph.IsEmpty)
            {
                _logger.LogInformation("There is nothing in the graph.");

                return;
            }

            var sensorMap = InitializeSensors(graph);

            while (_isLoopActive)
            {
                var propertyValueMap = Monitor(graph, sensorMap);
                // Monitor
                // Analyze
                // Plan
                // Execute

                // this should probably include some form of sleepy time
            }
        }

        private IGraph InitializeGraph(string filePath)
        {
            var turtleParser = new TurtleParser();
            var graph = new Graph();

            _logger.LogInformation("Loading instance model file contents from {filePath}...", filePath);

            try
            {
                turtleParser.Load(graph, filePath);
            }
            catch (Exception exception)
            {    
                _logger.LogError(exception, "Exception while loading file contents: {exceptionMessage}", exception.Message);
            }

            return graph;
        }

        private IDictionary<string, ISensor> InitializeSensors(IGraph graph)
        {
            var sensorMap = new Dictionary<string, ISensor>();

            var rdfType = graph.CreateUriNode(RdfPrefix + "type");
            var propertyClass = graph.CreateUriNode(SsnPrefix + "Property");
            var triples = graph.GetTriplesWithPredicateObject(rdfType, propertyClass);

            foreach (var triple in triples)
            {
                var propertyName = triple.Subject.ToString();

                // TODO: we can call the service provider after registering a factory for our sensors..
            }
        }

        private IDictionary<string, object> Monitor(IGraph graph, IDictionary<string, ISensor> sensorMap)
        {
            // query the graph and initialize the dictionary of properties
            // get the values for the properties from the corresponding sensors
            // execute the soft sensors with some properties as inputs
                // keep executing this as long as there are inputs remaining 
        }
    }
}
