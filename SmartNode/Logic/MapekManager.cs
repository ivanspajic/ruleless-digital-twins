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
        private const string XsdPrefix = "xsd";
        private const string XsdUri = "http://www.w3.org/2001/XMLSchema#";

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

        // Store objects in the cache to avoid re-querying.
        private readonly Dictionary<string, ObservableProperty> _observableProperties = [];
        private readonly Dictionary<string, InputOutput> _inputOutputs = [];
        private readonly Dictionary<string, ConfigurableParameter> _configurableParameters = [];
        private readonly Dictionary<string, OptimalCondition> _unfulfilledOptimalConditions = [];

        // A cache of Properties from the previous loop round. This could instead be saved in a more persistent way as
        // historical data.
        private readonly Dictionary<string, ObservableProperty> _oldObservableProperties = [];
        private readonly Dictionary<string, InputOutput> _oldInputOutputs = [];
        private readonly Dictionary<string, ConfigurableParameter> _oldConfigurableParameters = [];
        private readonly Dictionary<string, OptimalCondition> _oldUnfulfilledOptimalConditions = [];

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
            _query.Namespaces.AddNamespace(XsdPrefix, new Uri(XsdUri));
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
            var propertyValueType = GetInputOutputValueType(propertyNode);

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

        private string GetInputOutputValueType(INode propertyNode)
        {
            _query.CommandText = @"SELECT ?valueType WHERE {
                @property rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasValue .
                ?bNode owl:onDataRange ?valueType . }";

            return GetPropertyValueType("property", propertyNode);
        }

        private string GetObservablePropertyValueType(INode propertyNode)
        {
            _query.CommandText = @"SELECT ?valueType WHERE {
                @property rdf:type sosa:ObservableProperty .
                @property rdf:type ?bNode .
                ?bNode owl:onProperty meta:hasUpperLimitValue .
                ?bNode owl:onDataRange ?valueType . }";

            return GetPropertyValueType("property", propertyNode);
        }

        private string GetPropertyValueType(string parameterName, INode propertyNode)
        {
            _query.SetParameter(parameterName, propertyNode);

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

            // Get all ConfigurableParameters.
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
            // Get all ObservableProperties.
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

            // Get all OptimalConditions.
            _query.CommandText = @"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }";

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            foreach (var result in queryResult.Results)
            {
                var optimalCondition = result["optimalCondition"];
                var property = result["property"];
                var reachedInMaximumSeconds = result["reachedInMaximumSeconds"];
                var valueType = GetObservablePropertyValueType(property);

                _query.SetParameter("optimalCondition", optimalCondition);
                _query.SetParameter("property", property);
                _query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var constraints = ProcessConstraintQueries(result);
                EvaluateConstraints(property, valueType, constraints);
                // find the property value from the caches and use it for evaluation
                
                // evaluate all the constraints present, and add the optimal condition to the cache
                // in case of at least one constraint not being fulfilled.
                // in case of adding an optimal condition to the cache, query for all execution plans
                // that support regaining optimal conditions   
            }

            // TODO: make the executionplan cache!!
            // query for execution plans that optimize for stuff...
        }

        private List<Tuple<ConstraintOperator, string>> ProcessConstraintQueries(ISparqlResult result)
        {
            var constraints = new List<Tuple<ConstraintOperator, string>>();

            // Check if the OptimalCondition has a single-valued constraint.
            ProcessSingleValueEqualsConstraint(constraints);

            // Get all first values of constraint ranges with a '>' operator. This kind of query covers both
            // single-valued (e.g., >15) and double-valued (e.g. >15, <25) constraints.
            ProcessFirstValueGreaterThanConstraints(constraints);

            // Get all first values of constraint ranges with a '>=' operator.
            ProcessFirstValueGreaterThanOrEqualToConstraints(constraints);

            // Get all first values of constraint ranges with a '<' operator.
            ProcessFirstValueLessThanConstraints(constraints);

            // Get all first values of constraint ranges with a '<=' operator.
            ProcessFirstValueLessThanOrEqualToConstraints(constraints);

            // Get all second values of constraint ranges with a '>' operator.
            ProcessSecondValueGreaterThanConstraints(constraints);

            // Get all second values of constraint ranges with a '>=' operator.
            ProcessSecondValueGreaterThanOrEqualToConstraints(constraints);

            // Get all second values of constraint ranges with a '<' operator.
            ProcessSecondValueLessThanConstraints(constraints);

            // Get all second values of constraint ranges with a '<=' operator.
            ProcessSecondValueLessThanOrEqualToConstraints(constraints);

            // Get all negated single value constraints.
            ProcessNegatedSingleValueConstraints(constraints);

            // Get all negated first values of constraint ranges with a '>' operator. This kind of query covers both
            // single-valued (e.g., not(>15)) and double-valued (e.g. not(>15, <25)) constraints.
            ProcessNegatedFirstValueGreaterThanConstraint(constraints);

            // Get all negated first values of constraint ranges with a '>=' operator.
            ProcessNegatedFirstValueGreaterThanOrEqualToConstraint(constraints);

            // Get all negated first values of constraint ranges with a '<' operator.
            ProcessNegatedFirstValueLessThanConstraint(constraints);

            // Get all negated first values of constraint ranges with a '<=' operator.
            ProcessNegatedFirstValueLessThanOrEqualToConstraint(constraints);

            // Get all negated second values of constraint ranges with a '>' operator.
            ProcessNegatedSecondValueGreaterThanConstraint(constraints);

            // Get all negated second values of constraint ranges with a '>=' operator.
            ProcessNegatedSecondValueGreaterThanOrEqualToConstraint(constraints);

            // Get all negated second values of constraint ranges with a '<' operator.
            ProcessNegatedSecondValueLessThanConstraint(constraints);

            // Get all negated second values of constraint ranges with a '<=' operator.
            ProcessNegatedSecondValueLessThanOrEqualToConstraint(constraints);

            return constraints;
        }

        private void ProcessSingleValueEqualsConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:hasValue ?constraint . }";

            var singleValueQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            if (!singleValueQueryResult.IsEmpty)
            {
                // Reasoners (such as Protege's) should only allow one single value constraint per OptimalCondition.
                var constraint = singleValueQueryResult.Results[0]["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                var tuple = new Tuple<ConstraintOperator, string>(ConstraintOperator.EqualTo, constraint);
                constraints.Add(tuple);
            }
        }

        private void ProcessFirstValueGreaterThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessFirstValueGreaterThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessFirstValueLessThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessFirstValueLessThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessSecondValueGreaterThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessSecondValueGreaterThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessSecondValueLessThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessSecondValueLessThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedSingleValueConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:hasValue ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.NotEqualTo);
        }

        private void ProcessNegatedFirstValueGreaterThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                @optimalCondition ssn:forProperty @property .
                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                @optimalCondition rdf:type ?bNode1 .
                ?bNode1 owl:complementOf ?bNode2 .
                ?bNode2 owl:onProperty meta:hasValueConstraint .
                ?bNode2 owl:onDataRange ?bNode3 .
                ?bNode3 owl:withRestrictions ?bNode4 .
                ?bNode4 rdf:first ?anonymousNode .
                ?anonymousNode xsd:minExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedFirstValueGreaterThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessNegatedFirstValueLessThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessNegatedFirstValueLessThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessNegatedSecondValueGreaterThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedSecondValueGreaterThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessNegatedSecondValueLessThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessNegatedSecondValueLessThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ExecuteAndProcessOptimalConditionConstraintQueryResult(List<Tuple<ConstraintOperator, string>> constraints,
            ConstraintOperator constraintOperator)
        {
            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            foreach (var innerResult in queryResult)
            {
                var constraint = innerResult["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                var tuple = new Tuple<ConstraintOperator, string>(constraintOperator, constraint);
                constraints.Add(tuple);
            }
        }

        private void EvaluateConstraints(INode propertyNode, string valueType, List<Tuple<ConstraintOperator, string>> constraints)
        {
            var propertyName = propertyNode.ToString();

            if (_configurableParameters.TryGetValue(propertyName, out ConfigurableParameter configurableParameter))
            {

            }
            else if (_inputOutputs.TryGetValue(propertyName, out InputOutput inputOutput))
            {

            }
            else if (_observableProperties.TryGetValue(propertyName, out ObservableProperty observableProperty))
            {
                
            }
            else
            {
                _logger.LogError("Property {property} was not found in the system.", propertyName);

                throw new Exception("The Property must be in the system to be a part of an OptimalCondition.");
            }
        }

        #endregion
    }
}
