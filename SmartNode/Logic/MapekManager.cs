using Logic.DeviceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace Logic
{
    public class MapekManager : IMapekManager
    {
        private const string DtPrefix = "http://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation/";
        private const string SosaPrefix = "http://www.w3.org/ns/sosa/";
        private const string SsnPrefix = "http://www.w3.org/ns/ssn/";
        private const string RdfPrefix = "rdf:";
        private const string OwlPrefix = "owl:";

        private bool _isLoopActive = false;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MapekManager> _logger;
        private readonly Func<string, ISensor> _sensorFactory;

        public MapekManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<MapekManager>>();
            _sensorFactory = _serviceProvider.GetRequiredService<Func<string, ISensor>>();
        }

        public void StartLoop(string filePath)
        {
            _logger.LogInformation("the path is {path}", filePath);

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

            // Load the instance model into a graph object.
            IGraph graph = InitializeGraph(filePath);
            
            // If nothing was loaded, don't start the loop.
            if (graph.IsEmpty)
            {
                _logger.LogInformation("There is nothing in the graph. Terminated MAPE-K loop.");

                _isLoopActive = false;
            }

            while (_isLoopActive)
            {
                var propertyValuesTuple = Monitor(graph);
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
                turtleParser.Load(graph, filePath); // we have to ditch webassembly for this to work. docker would make more sense
            }
            catch (Exception exception)
            {    
                _logger.LogError(exception, "Exception while loading file contents: {exceptionMessage}", exception.Message);
            }

            return graph;
        }


        private Tuple<IDictionary<string, Tuple<object, object>>, IDictionary<string, object>> Monitor(IGraph graph)
        {
            // Two collections of property values are necessary since properties observed by hard sensors will only have
            // estimated values within some range, as dictated by the devices that measure it. For example, a room
            // temperature could be measured by two sensors, each reporting a slightly different value. In our ontology
            // (based on SOSA/SSN), these measured property values are Outputs of Procedures implemented by Sensors. As
            // a result, the original observed room temperature property would have a possible value range between a
            // minimum and a maximum, as dictated by the two slightly different sensor measurements.
            var observablePropertyMap = new Dictionary<string, Tuple<object, object>>();
            var computedPropertyMap = new Dictionary<string, object>();
            var propertyValuesTuple = new Tuple<IDictionary<string, Tuple<object, object>>,
                IDictionary<string, object>>(observablePropertyMap, computedPropertyMap);

            var queryResult = (SparqlResultSet)graph.ExecuteQuery("SELECT ?observableProperty WHERE" +
                "?observableProperty rdf:type Property." +
                "?observableProperty sosa:isObservedBy ?sensor." +
                "?sensor rdf:type Sensor.");



            return propertyValuesTuple;
        }
    }
}
