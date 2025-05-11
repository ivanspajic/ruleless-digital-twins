using Logic.DeviceInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Logic
{
    public class MapekManager : IMapekManager
    {
        private const string DtPrefix = "meta";
        private const string DtUri = "http://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation/";
        private const string SosaPrefix = "sosa";
        private const string SosaUri = "http://www.w3.org/ns/sosa/";
        private const string SsnPrefix = "ssn";
        private const string SsnUri = "http://www.w3.org/ns/ssn/";
        private const string RdfPrefix = "rdf";
        private const string RdfUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private const string OwlPrefix = "owl";
        private const string OwlUri = "http://www.w3.org/2002/07/owl#";

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
            _logger.LogInformation("Starting the MAPE-K loop.");

            while (_isLoopActive)
            {
                // Load the instance model into a graph object. Doing this inside the loop allows for dynamic model updates at
                // runtime.
                IGraph graph = InitializeGraph(filePath);

                // If nothing was loaded, don't start the loop.
                if (graph.IsEmpty)
                {
                    _logger.LogInformation("There is nothing in the graph. Terminated MAPE-K loop.");

                    _isLoopActive = false;
                }

                var propertyValuesTuple = Monitor(graph);
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

            _logger.LogInformation("Loading instance model file contents from {filePath}.", filePath);

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


        private Tuple<IDictionary<string, Tuple<object, object>>, IDictionary<string, object>> Monitor(IGraph graph)
        {
            _logger.LogInformation("Starting the Monitor phase.");

            // Two collections of property values are necessary since properties observed by hard sensors will only have
            // estimated values within some range, as dictated by the devices that measure it. For example, a room
            // temperature could be measured by two sensors, each reporting a slightly different value. In our ontology
            // (based on SOSA/SSN), these measured property values are Outputs of Procedures implemented by Sensors. As
            // a result, the original observed room temperature property would have a possible value range between a
            // minimum and a maximum, as dictated by the two slightly different sensor measurements.
            var observablePropertyMap = new Dictionary<string, Tuple<object, object>>();
            var measuredPropertyMap = new Dictionary<string, object>();
            var propertyValuesTuple = new Tuple<IDictionary<string, Tuple<object, object>>,
                IDictionary<string, object>>(observablePropertyMap, measuredPropertyMap);

            var query = new SparqlParameterizedString();

            query.Namespaces.AddNamespace(DtPrefix, new Uri(DtUri));
            query.Namespaces.AddNamespace(SosaPrefix, new Uri(SosaUri));
            query.Namespaces.AddNamespace(SsnPrefix, new Uri(SsnUri));
            query.Namespaces.AddNamespace(RdfPrefix, new Uri(RdfUri));
            query.Namespaces.AddNamespace(OwlPrefix, new Uri(OwlUri));

            // Gets all measured Properties (Inputs and Outputs) of all sensors.
            query.CommandText = "SELECT ?measuredProperty ?sensor WHERE {" +
                "?sensor rdf:type sosa:Sensor ." +
                "?sensor ssn:implements ?procedure ." +
                "?procedure ssn:hasOutput ?measuredProperty . }";

            var queryResult = (SparqlResultSet)graph.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var measuredPropertyName = result["measuredProperty"].ToString();
                var sensorName = result["sensor"].ToString();
                // var measuredPropertyValue = 
                // TODO: this whole section needs to be put into the recursive method to facilitate populating the dictionary
                // while only executing the respective sensors for each measured property once

                measuredPropertyMap.Add(measuredPropertyName, measuredPropertyValue);
            }

            // Gets all ObservableProperties observed by hard Sensors and their value types. Note that ontologically,
            // soft Sensors make their observations as Inputs from hard Sensors.
            query.CommandText = "SELECT DISTINCT ?observableProperty ?sensor WHERE {" +
                "?observableProperty rdf:type sosa:ObservableProperty ." +
                "?sensor rdf:type sosa:Sensor ." +
                "?sensor sosa:observes ?observableProperty . }";

            queryResult = (SparqlResultSet)graph.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var observablePropertyName = result["observableProperty"].ToString();
                var sensorName = result["sensor"].ToString();
                
                // you can only get the values for the tuple once you have the measured values of the hard sensors
            }

            return propertyValuesTuple;
        }

        private object 
    }
}
