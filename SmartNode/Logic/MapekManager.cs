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

        private const string IntegerTypeName = "int";
        private const string DoubleTypeName = "double";

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

            // Get all measured Properties that aren't inputs to other soft sensors. Since soft Sensors may use other
            // Sensors' Outputs as their own Inputs, this query effectively gets the roots of the Sensor trees in the system.
            query.CommandText = @"SELECT ?measuredProperty WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput ?measuredProperty .
                FILTER NOT EXISTS { ?measuredProperty meta:isInputOf ?otherProcedure } . }";

            var queryResult = (SparqlResultSet)graph.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var measuredProperty = result["measuredProperty"];
                PopulateMeasuredPropertyMap(measuredProperty, graph, query, measuredPropertyMap);
            }

            PopulateObservablePropertyMap(graph, query, observablePropertyMap);

            return propertyValuesTuple;
        }

        private void PopulateMeasuredPropertyMap(INode measuredProperty,
            IGraph graph,
            SparqlParameterizedString query,
            IDictionary<string, object> measuredPropertyMap)
        {
            // Simply return if the current measured Property already exists in the map. This is necessary to avoid
            // unnecessary multiple executions of the same Sensors since a single measured Property can be an Input to
            // multiple soft Sensors.
            if (measuredPropertyMap.ContainsKey(measuredProperty.ToString()))
                return;

            // Get all Sensors that have @measuredProperty as their Output. SOSA/SSN theoretically allows for multiple
            // Sensors (Procedures) to have the same Output due to a lack of cardinality restrictions on the inverse
            // predicate of 'has output' in the definition of Output.
            query.CommandText = @"SELECT ?sensor WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput @measuredProperty . }";

            query.SetParameter("measuredProperty", measuredProperty);

            var queryResult = (SparqlResultSet)graph.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var sensorNode = result["sensor"];
                // Get an instance of a Sensor from the Sensor factory.
                var sensor = _sensorFactory(sensorNode.ToString());

                // Get all measured Properties this Sensor uses as its Inputs.
                query.CommandText = @"SELECT ?inputProperty WHERE {
                    @sensor ssn:implements ?procedure .
                    ?procedure ssn:hasInput ?inputProperty . }";

                query.SetParameter("sensor", sensorNode);

                var innerQueryResult = (SparqlResultSet)graph.ExecuteQuery(query);

                // Construct the required input Property array.
                var inputProperties = new object[innerQueryResult.Count];

                // For each Input Property, call this method recursively and record the value from the map in inputProperties
                // for the current Sensor to use on invocation. In case of no Inputs, the array remains empty.
                for (var i = 0; i < innerQueryResult.Results.Count; i++)
                {
                    var inputProperty = innerQueryResult.Results[i]["inputProperty"];
                    PopulateMeasuredPropertyMap(inputProperty, graph, query, measuredPropertyMap);

                    inputProperties[i] = measuredPropertyMap[inputProperty.ToString()];
                }

                // Invoke the Sensor with the corresponding Inputs and save the returned value in the map.
                var measuredPropertyValue = sensor.ObservePropertyValue(inputProperties);
                measuredPropertyMap.Add(measuredProperty.ToString(), measuredPropertyValue);
            }
        }

        private void PopulateObservablePropertyMap(IGraph graph,
            SparqlParameterizedString query,
            IDictionary<string, Tuple<object, object>> observablePropertyMap,
            IDictionary<string, object> measuredPropertyMap)
        {
            // Get all ObservableProperties.
            query.CommandText = @"SELECT ?observableProperty ?valueType WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . 
                ?observableProperty meta:hasValue ?valueType . }";

            var queryResult = (SparqlResultSet)graph.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var observableProperty = result["observableProperty"];
                var valueType = result["valueType"].ToString();

                // Get all measured Properties that are Outputs of Sensor Procedures measuring the current ObservableProperty.
                query.CommandText = @"SELECT ?measuredProperty WHERE {
                    ?sensor sosa:observes @observableProperty .
                    ?sensor ssn:implements ?procedure .
                    ?procedure ssn:hasOutput ?measuredProperty . }";

                query.SetParameter("observableProperty", observableProperty);

                var innerQueryResult = (SparqlResultSet)graph.ExecuteQuery(query);
                var rangeTuple = FindObservablePropertyValueRange(innerQueryResult, valueType, measuredPropertyMap);

                observablePropertyMap.Add(observableProperty.ToString(), rangeTuple);
            }
        }

        private Tuple<object, object> FindObservablePropertyValueRange(SparqlResultSet queryResult,
            string valueType,
            IDictionary<string, object> measuredPropertyMap)
        {
            if (valueType == IntegerTypeName)
            {

            }
            else if (valueType == DoubleTypeName)
            {

            }

            // TODO: finish this range finder in a scalable way...
        }
    }
}
