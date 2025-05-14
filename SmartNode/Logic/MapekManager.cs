using Logic.DeviceInterfaces;
using Logic.SensorValueHandlers;
using Lucene.Net.Search;
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

        private const int SleepyTimeMilliseconds = 5_000;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MapekManager> _logger;

        private readonly Func<string, ISensor> _sensorFactory;

        // Contains supported XMl/RDF/OWL types and their respective value handlers. As new types
        // become supported, their name/implementation instance pair is simply added to the collection.
        private readonly Dictionary<string, ISensorValueHandler> _sensorValueHandlers = new()
        {
            { "int", new SensorIntValueHandler() },
            { "double", new SensorDoubleValueHandler() }
        };

        // Store Property values in collections for caching to avoid re-querying.
        //
        // Two collections of property values are necessary since properties observed by hard sensors will only have
        // estimated values within some range, as dictated by the devices that measure it. For example, a room
        // temperature could be measured by two sensors, each reporting a slightly different value. In our ontology
        // (based on SOSA/SSN), these measured property values are Outputs of Procedures implemented by Sensors. As
        // a result, the original observed room temperature property would have a possible value range between a
        // minimum and a maximum, as dictated by the two slightly different sensor measurements.
        private readonly Dictionary<string, Tuple<object, object>> _observablePropertyValues = [];
        private readonly Dictionary<string, object> _measuredPropertyValues = [];

        private bool _isLoopActive = false;

        private Graph _instanceModelGraph = new();

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
                Initialize(filePath);

                // If nothing was loaded, don't start the loop.
                if (_instanceModelGraph.IsEmpty)
                {
                    _logger.LogError("There is nothing in the graph. Terminated MAPE-K loop.");

                    throw new Exception("The graph is empty.");
                }

                // Observe all hard and soft Sensor values.
                Monitor();
                // Out of all possible ExecutionPlans, filter out the irrelevant ones based on current Property values.
                Analyze();
                // Plan
                // Execute

                Thread.Sleep(SleepyTimeMilliseconds);
            }
        }

        private void Initialize(string filePath)
        {            
            // Reset the cache.
            _instanceModelGraph.Clear();
            _observablePropertyValues.Clear();
            _measuredPropertyValues.Clear();

            _logger.LogInformation("Loading instance model file contents from {filePath}.", filePath);

            var turtleParser = new TurtleParser();
            try
            {
                turtleParser.Load(_instanceModelGraph, filePath);
            }
            catch (Exception exception)
            {    
                _logger.LogError(exception, "Exception while loading file contents: {exceptionMessage}", exception.Message);

                throw;
            }
        }

        #region Monitor
        private void Monitor()
        {
            _logger.LogInformation("Starting the Monitor phase.");

            
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

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var measuredProperty = result["measuredProperty"];
                PopulateMeasuredPropertyMap(measuredProperty, query);
            }

            PopulateObservablePropertyMap(query);
        }

        private void PopulateMeasuredPropertyMap(INode measuredProperty, SparqlParameterizedString query)
        {
            // Simply return if the current measured Property already exists in the map. This is necessary to avoid
            // unnecessary multiple executions of the same Sensors since a single measured Property can be an Input to
            // multiple soft Sensors.
            if (_measuredPropertyValues.ContainsKey(measuredProperty.ToString()))
                return;

            // Get all Sensors that have @measuredProperty as their Output. SOSA/SSN theoretically allows for multiple
            // Sensors (Procedures) to have the same Output due to a lack of cardinality restrictions on the inverse
            // predicate of 'has output' in the definition of Output.
            query.CommandText = @"SELECT ?sensor WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput @measuredProperty . }";

            query.SetParameter("measuredProperty", measuredProperty);

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(query);

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

                var innerQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(query);

                // Construct the required input Property array.
                var inputProperties = new object[innerQueryResult.Count];

                // For each Input Property, call this method recursively and record the value from the map in inputProperties
                // for the current Sensor to use on invocation. In case of no Inputs, the array remains empty.
                for (var i = 0; i < innerQueryResult.Results.Count; i++)
                {
                    var inputProperty = innerQueryResult.Results[i]["inputProperty"];
                    PopulateMeasuredPropertyMap(inputProperty, query);

                    inputProperties[i] = _measuredPropertyValues[inputProperty.ToString()];
                }

                // Invoke the Sensor with the corresponding Inputs and save the returned value in the map.
                var measuredPropertyValue = sensor.ObservePropertyValue(inputProperties);
                _measuredPropertyValues.Add(measuredProperty.ToString(), measuredPropertyValue);
            }
        }

        private void PopulateObservablePropertyMap(SparqlParameterizedString query)
        {
            // Get all ObservableProperties.
            query.CommandText = @"SELECT DISTINCT ?observableProperty ?valueType WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . 
                ?observableProperty rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasValue .
                ?bNode owl:onDataRange ?valueType . }";

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var observableProperty = result["observableProperty"];
                var valueTypeSchema = result["valueType"].ToString();
                var valueType = valueTypeSchema.Split('#')[1];

                ISensorValueHandler sensorValueHandler;
                try
                {
                    sensorValueHandler = _sensorValueHandlers[valueType];
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "No supported .NET type implementation found for XML/RDF/OWL type {type}.", valueType);

                    throw;
                }

                // Get all measured Properties that are Outputs of Sensor Procedures measuring the current ObservableProperty.
                query.CommandText = @"SELECT ?measuredProperty WHERE {
                    ?sensor sosa:observes @observableProperty .
                    ?sensor ssn:implements ?procedure .
                    ?procedure ssn:hasOutput ?measuredProperty . }";

                query.SetParameter("observableProperty", observableProperty);

                var innerQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(query);
                var rangeTuple = sensorValueHandler.FindObservablePropertyValueRange(innerQueryResult, "measuredProperty", _measuredPropertyValues);

                _observablePropertyValues.Add(observableProperty.ToString(), rangeTuple);
            }
        }
        #endregion

        #region Analyze
        public void Analyze()
        {
            // Figure out where we are with respect to the OptimalConditions.
            // Depending on the effect needed, filter out the irrelevant ExecutionPlans.
        }
        #endregion
    }
}
