using Logic.DeviceInterfaces;
using Logic.SensorValueHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
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

        private readonly Func<string, string, ISensor> _sensorFactory;

        // Contains supported XMl/RDF/OWL types and their respective value handlers. As new types become supported, their
        // name/implementation instance pair is simply added to the collection.
        private readonly Dictionary<string, ISensorValueHandler> _sensorValueHandlers = new()
        {
            { "double", new SensorDoubleValueHandler() },
            { "int", new SensorIntValueHandler() }
        };

        // Store Property values in collections for caching to avoid re-querying.
        private readonly Dictionary<string, ObservableProperty> _observableProperties = [];
        private readonly Dictionary<string, InputOutput> _inputOutputs = [];
        private readonly Dictionary<string, ConfigurableParameter> _configurableParameters = [];

        // A cache of Properties from the previous loop round. This could instead be saved in a more persistent way as
        // historical data.
        private readonly Dictionary<string, ObservableProperty> _oldObservableProperties = [];
        private readonly Dictionary<string, InputOutput> _oldInputOutputs = [];
        private readonly Dictionary<string, ConfigurableParameter> _oldConfigurableParameters = [];

        private readonly Graph _instanceModelGraph = new();
        private readonly SparqlParameterizedString _query = new();

        private bool _isLoopActive = false;

        public MapekManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<MapekManager>>();
            _sensorFactory = _serviceProvider.GetRequiredService<Func<string, string, ISensor>>();

            // Register the relevant prefixes for the queries to come.
            _query.Namespaces.AddNamespace(DtPrefix, new Uri(DtUri));
            _query.Namespaces.AddNamespace(SosaPrefix, new Uri(SosaUri));
            _query.Namespaces.AddNamespace(SsnPrefix, new Uri(SsnUri));
            _query.Namespaces.AddNamespace(RdfPrefix, new Uri(RdfUri));
            _query.Namespaces.AddNamespace(OwlPrefix, new Uri(OwlUri));
        }

        public void StartLoop(string instanceModelFilePath)
        {
            _isLoopActive = true;

            RunMapekLoop(instanceModelFilePath);
        }

        public void StopLoop()
        {
            _isLoopActive = false;
        }

        private void RunMapekLoop(string instanceModelFilePath)
        {
            _logger.LogInformation("Starting the MAPE-K loop.");

            while (_isLoopActive)
            {
                // Load the instance model into a graph object. Doing this inside the loop allows for dynamic model updates at
                // runtime.
                Initialize(instanceModelFilePath);

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

        private void Initialize(string instanceModelFilePath)
        {
            ResetCaches();

            _logger.LogInformation("Loading instance model file contents from {filePath}.", instanceModelFilePath);

            var turtleParser = new TurtleParser();
            try
            {
                turtleParser.Load(_instanceModelGraph, instanceModelFilePath);
            }
            catch (Exception exception)
            {    
                _logger.LogError(exception, "Exception while loading file contents: {exceptionMessage}", exception.Message);

                throw;
            }
        }

        private void ResetCaches()
        {
            _instanceModelGraph.Clear();
            _observableProperties.Clear();
            _inputOutputs.Clear();
            _configurableParameters.Clear();
        }

        #region Monitor
        private void Monitor()
        {
            _logger.LogInformation("Starting the Monitor phase.");

            // Get all measured Properties (Sensor Outputs) that aren't Inputs to other soft Sensors. Since soft Sensors may use
            // other Sensors' Outputs as their own Inputs, this query effectively gets the roots of the Sensor trees in the system.
            _query.CommandText = @"SELECT ?property WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput ?property .
                FILTER NOT EXISTS { ?property meta:isInputOf ?otherProcedure } . }";

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            // Get the values of all measured Properties (Sensor Inputs/Outputs and ConfigurableParameters) and populate the
            // cache.
            foreach (var result in queryResult.Results)
            {
                var property = result["property"];
                PopulateInputOutputsAndConfigurableParametersCaches(property);
            }

            // Get the values of all ObservableProperties and populate the cache.
            PopulateObservablePropertiesCache();
        }

        private void PopulateInputOutputsAndConfigurableParametersCaches(INode propertyNode)
        {
            var propertyName = propertyNode.ToString();

            // Simply return if the current Property already exists in the cache. This is necessary to avoid unnecessary multiple
            // executions of the same Sensors since a single Property can be an Input to multiple soft Sensors.
            if (_inputOutputs.ContainsKey(propertyName) || _configurableParameters.ContainsKey(propertyName))
                return;

            // Get the type of the Property.
            var propertyValueType = GetPropertyValueType(propertyNode);

            // Get all Procedures (in Sensors) that have @property as their Output. SOSA/SSN theoretically allows for multiple Procedures
            // to have the same Output due to a lack of cardinality restrictions on the inverse predicate of 'has output' in the
            // definition of Output.
            _query.CommandText = @"SELECT ?procedure ?sensor WHERE {
                ?procedure ssn:hasOutput @property .
                ?sensor ssn:implements ?procedure .
                ?sensor rdf:type sosa:Sensor . }";

            _query.SetParameter("property", propertyNode);

            var procedureQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);
            
            // If the current Property is not an Output of any other Procedures, then it must be a ConfigurableParameter.
            if (procedureQueryResult.IsEmpty)
            {
                AddConfigurableParameterToCache(propertyNode);

                return;
            }

            // Otherwise, for each Procedure, find the Inputs.
            foreach (var result in procedureQueryResult.Results)
            {
                var procedureNode = result["procedure"];
                var sensorNode = result["sensor"];
                // Get an instance of a Sensor from the Sensor factory.
                var sensor = _sensorFactory(sensorNode.ToString(), procedureNode.ToString());

                // Get all measured Properties this Sensor uses as its Inputs.
                _query.CommandText = @"SELECT ?inputProperty WHERE {
                    @procedure ssn:hasInput ?inputProperty .
                    @sensor ssn:implements @procedure . }";

                _query.SetParameter("procedure", procedureNode);
                _query.SetParameter("sensor", sensorNode);

                var innerQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

                // Construct the required Input Property array.
                var inputProperties = new object[innerQueryResult.Count];

                // For each Input Property, call this method recursively and record the newly-cached value in inputProperties
                // for the current Sensor to use on invocation. In case of no Inputs, the inputProperties array remains empty.
                for (var i = 0; i < innerQueryResult.Results.Count; i++)
                {
                    var inputProperty = innerQueryResult.Results[i]["inputProperty"];
                    PopulateInputOutputsAndConfigurableParametersCaches(inputProperty);

                    if (_inputOutputs.ContainsKey(inputProperty.ToString()))
                        inputProperties[i] = _inputOutputs[inputProperty.ToString()].Value;
                    else if (_configurableParameters.ContainsKey(inputProperty.ToString()))
                        inputProperties[i] = _configurableParameters[inputProperty.ToString()].Value;
                    else
                    {
                        _logger.LogError("The Input Property {property} was not found in the respective Property caches.", inputProperty.ToString());

                        throw new Exception("The Property tree traversal didn't populate the caches with all properties.");
                    }
                }

                // Invoke the Sensor with the corresponding Inputs and save the returned value in the map.
                var propertyValue = sensor.ObservePropertyValue(inputProperties);
                var property = new InputOutput
                {
                    Name = propertyNode.ToString(),
                    OwlType = propertyValueType,
                    Value = propertyValue
                };

                _inputOutputs.Add(property.Name, property);
            }
        }

        private string GetPropertyValueType(INode propertyNode)
        {
            _query.CommandText = @"SELECT ?valueType WHERE {
                @property rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasValue .
                ?bNode owl:onDataRange ?valueType . }";
            _query.SetParameter("property", propertyNode);

            var propertyTypeQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            if (propertyTypeQueryResult.IsEmpty)
            {
                _logger.LogError("The Property {property} has no value type.", propertyNode.ToString());

                throw new Exception("A property was found without a value type.");
            }

            var propertyValueType = propertyTypeQueryResult.Results[0]["valueType"].ToString();
            return propertyValueType.Split('#')[1];
        }

        private void AddConfigurableParameterToCache(INode propertyNode)
        {
            var propertyName = propertyNode.ToString();

            _query.CommandText = @"SELECT ?lowerLimit ?upperLimit ?valueIncrements WHERE {
                    @property rdf:type meta:ConfigurableParameter .
                    @property meta:hasLowerLimitValue ?lowerLimit .
                    @property meta:hasUpperLimitValue ?upperLimit .
                    @property meta:hasValueIncrements ?valueIncrements . }";

            _query.SetParameter("property", propertyNode);

            var configurableParameterQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            // If the Property isn't a ConfigurableParameter, throw an error.
            if (configurableParameterQueryResult.IsEmpty)
            {
                _logger.LogError("The Property {property} was not found as an Output nor as a ConfigurableParameter.", propertyName);

                throw new Exception("The Property must exist as an ObservableProperty, an Output, or a ConfigurableParameter.");
            }

            if (_oldConfigurableParameters.TryGetValue(propertyName, out ConfigurableParameter? value))
            {
                _configurableParameters.Add(propertyName, value);

                return;
            }

            var lowerLimit = configurableParameterQueryResult.Results[0]["lowerLimit"].ToString();
            lowerLimit = lowerLimit.Split('^')[0];
            var upperLimit = configurableParameterQueryResult.Results[0]["upperLimit"].ToString();
            upperLimit = upperLimit.Split('^')[0];
            var valueIncrements = configurableParameterQueryResult.Results[0]["valueIncrements"].ToString();
            valueIncrements = valueIncrements.Split('^')[0];

            // Instantiate the new ConfigurableParameter with its lower limit as its value and add it to the cache.
            var configurableParameter = new ConfigurableParameter
            {
                Name = propertyName,
                LowerLimitValue = lowerLimit,
                UpperLimitValue = upperLimit,
                ValueIncrements = valueIncrements,
                Value = lowerLimit
            };

            _configurableParameters.Add(propertyName, configurableParameter);
        }

        private void PopulateObservablePropertiesCache()
        {
            _query.CommandText = @"SELECT DISTINCT ?observableProperty ?valueType WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty .
                ?observableProperty rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasValue .
                ?bNode owl:onDataRange ?valueType . }";

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            foreach (var result in queryResult.Results)
            {
                var observablePropertyNode = result["observableProperty"];
                var valueType = result["valueType"].ToString();
                valueType = valueType.Split('#')[1];

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
                _query.CommandText = @"SELECT ?measuredProperty WHERE {
                    ?sensor sosa:observes @observableProperty .
                    ?sensor ssn:implements ?procedure .
                    ?procedure ssn:hasOutput ?measuredProperty . }";

                _query.SetParameter("observableProperty", observablePropertyNode);

                var innerQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);
                var rangeTuple = sensorValueHandler.FindObservablePropertyValueRange(innerQueryResult, "measuredProperty", _inputOutputs);
                var observableProperty = new ObservableProperty
                {
                    Name = observablePropertyNode.ToString(),
                    OwlType = valueType,
                    LowerLimitValue = rangeTuple.Item1,
                    UpperLimitValue = rangeTuple.Item2
                };

                _observableProperties.Add(observablePropertyNode.ToString(), observableProperty);
            }
        }
        #endregion

        #region Analyze
        public void Analyze()
        {
            _logger.LogInformation("Starting the Analyze phase.");

            _query.CommandText = @"SELECT ?optimalCondition ?property WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property . }";

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            foreach (var result in queryResult.Results)
            {
                var optimalConditionName = result["optimalCondition"].ToString();
                var propertyName = result["property"].ToString();

                _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition ";

                // TODO: get the constraint, represent it via a standardized way (probably expressions) and execute it through the
                // specific type value handlers to validate it against the current value(s) of the respective property. depending on
                // this result, you can query for the right kind of executionplan and save it in the cache.
                //
                // TODO: make the executionplan cache!!
            }

            // get optimal conditions together with their properties
            // compare the values of each optimal condition to the values of the respective property
            // depending on the status, get the right kind of executionplan by comparing their effects with what is needed
            // these may include both optimizing plans as well as plans for regaining optimal conditions

            // this is done best by looking at each constraint in each optimal condition on its own
            // by evaluating each constraint separately, it becomes clear where with respect to the constraint ranges we are and thus what
            // kind of executionplans need to be included

            
        }
        #endregion
    }
}
