using Logic.FactoryInterface;
using Logic.SensorValueHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.MapekModels;
using VDS.RDF;
using VDS.RDF.Query;

namespace Logic.Mapek
{
    public class MapekMonitor : IMapekMonitor
    {
        private readonly ILogger<MapekMonitor> _logger;
        private readonly IFactory _factory;

        private PropertyCache _oldPropertyCache;

        public MapekMonitor(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekMonitor>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();

            _oldPropertyCache = new PropertyCache
            {
                ComputableProperties = new Dictionary<string, InputOutput>(),
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                ObservableProperties = new Dictionary<string, ObservableProperty>()
            };
        }

        public PropertyCache Monitor(IGraph instanceModel)
        {
            _logger.LogInformation("Starting the Monitor phase.");

            var propertyCache = new PropertyCache
            {
                ComputableProperties = new Dictionary<string, InputOutput>(),
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                ObservableProperties = new Dictionary<string, ObservableProperty>()
            };

            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all measured Properties (Sensor Outputs) that aren't Inputs to other soft Sensors. Since soft Sensors may use
            // other Sensors' Outputs as their own Inputs, this query effectively gets the roots of the Sensor trees in the system.
            query.CommandText = @"SELECT ?property WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput ?property .
                FILTER NOT EXISTS { ?property meta:isInputOf ?otherProcedure } . }";

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            // Get the values of all measured Properties (Sensor Inputs/Outputs and ConfigurableParameters) and populate the
            // cache.
            foreach (var result in queryResult.Results)
            {
                var property = result["property"];
                PopulateInputOutputsAndConfigurableParametersCaches(instanceModel, property, propertyCache);
            }

            // Get the values of all ObservableProperties and populate the cache.
            PopulateObservablePropertiesCache(instanceModel, propertyCache);

            _oldPropertyCache = propertyCache;

            return propertyCache;
        }

        private void PopulateInputOutputsAndConfigurableParametersCaches(IGraph instanceModel, INode propertyNode, PropertyCache propertyCache)
        {
            var propertyName = propertyNode.ToString();

            // Simply return if the current Property already exists in the cache. This is necessary to avoid unnecessary multiple
            // executions of the same Sensors since a single Property can be an Input to multiple soft Sensors.
            if (propertyCache.ComputableProperties.ContainsKey(propertyName) || propertyCache.ConfigurableParameters.ContainsKey(propertyName))
                return;

            // Get the type of the Property.
            var propertyType = MapekUtilities.GetPropertyValueType(instanceModel, propertyNode);

            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all Procedures (in Sensors) that have @property as their Output. SOSA/SSN theoretically allows for multiple Procedures
            // to have the same Output due to a lack of cardinality restrictions on the inverse predicate of 'has output' in the
            // definition of Output.
            query.CommandText = @"SELECT ?procedure ?sensor WHERE {
                ?procedure ssn:hasOutput @property .
                ?sensor ssn:implements ?procedure .
                ?sensor rdf:type sosa:Sensor . }";

            query.SetParameter("property", propertyNode);

            var procedureQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            // If the current Property is not an Output of any other Procedures, then it must be a ConfigurableParameter.
            if (procedureQueryResult.IsEmpty)
            {
                AddConfigurableParameterToCache(instanceModel, propertyNode, propertyType, propertyCache);

                return;
            }

            // Otherwise, for each Procedure, find the Inputs.
            foreach (var result in procedureQueryResult.Results)
            {
                var procedureNode = result["procedure"];
                var sensorNode = result["sensor"];
                // Get an instance of a Sensor from the factory.
                var sensor = _factory.GetSensorImplementation(sensorNode.ToString(), procedureNode.ToString());

                query = MapekUtilities.GetParameterizedStringQuery();

                // Get all measured Properties this Sensor uses as its Inputs.
                query.CommandText = @"SELECT ?inputProperty WHERE {
                    @procedure ssn:hasInput ?inputProperty .
                    @sensor ssn:implements @procedure . }";

                query.SetParameter("procedure", procedureNode);
                query.SetParameter("sensor", sensorNode);

                var innerQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

                // Construct the required Input Property array.
                var inputProperties = new object[innerQueryResult.Count];

                // For each Input Property, call this method recursively and record the newly-cached value in inputProperties
                // for the current Sensor to use on invocation. In case of no Inputs, the inputProperties array remains empty.
                for (var i = 0; i < innerQueryResult.Results.Count; i++)
                {
                    var inputProperty = innerQueryResult.Results[i]["inputProperty"];
                    PopulateInputOutputsAndConfigurableParametersCaches(instanceModel, inputProperty, propertyCache);

                    if (propertyCache.ComputableProperties.ContainsKey(inputProperty.ToString()))
                        inputProperties[i] = propertyCache.ComputableProperties[inputProperty.ToString()].Value;
                    else if (propertyCache.ConfigurableParameters.ContainsKey(inputProperty.ToString()))
                        inputProperties[i] = propertyCache.ConfigurableParameters[inputProperty.ToString()].Value;
                    else
                    {
                        _logger.LogError("The Input Property {property} was not found in the respective Property caches.",
                            inputProperty.ToString());

                        throw new Exception("The Property tree traversal didn't populate the caches with all properties.");
                    }
                }

                // Invoke the Sensor with the corresponding Inputs and save the returned value in the map.
                var propertyValue = sensor.ObservePropertyValue(inputProperties);
                var property = new InputOutput
                {
                    Name = propertyNode.ToString(),
                    OwlType = propertyType,
                    Value = propertyValue
                };

                propertyCache.ComputableProperties.Add(property.Name, property);
            }
        }

        private void AddConfigurableParameterToCache(IGraph instanceModel,
            INode propertyNode,
            string propertyType,
            PropertyCache propertyCache)
        {
            var propertyName = propertyNode.ToString();

            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all ConfigurableParameters.
            query.CommandText = @"SELECT ?lowerLimit ?upperLimit ?valueIncrements WHERE {
                    @property rdf:type meta:ConfigurableParameter .
                    @property meta:hasLowerLimitValue ?lowerLimit .
                    @property meta:hasUpperLimitValue ?upperLimit .
                    @property meta:hasValueIncrements ?valueIncrements . }";

            query.SetParameter("property", propertyNode);

            var configurableParameterQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            // If the Property isn't a ConfigurableParameter, throw an error.
            if (configurableParameterQueryResult.IsEmpty)
            {
                _logger.LogError("The Property {property} was not found as an Output nor as a ConfigurableParameter.", propertyName);

                throw new Exception("The Property must exist as an ObservableProperty, an Output, or a ConfigurableParameter.");
            }

            if (_oldPropertyCache.ConfigurableParameters.TryGetValue(propertyName, out ConfigurableParameter? value))
            {
                propertyCache.ConfigurableParameters.Add(propertyName, value);

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
                Value = lowerLimit,
                OwlType = propertyType
            };

            propertyCache.ConfigurableParameters.Add(propertyName, configurableParameter);
        }

        private void PopulateObservablePropertiesCache(IGraph instanceModel, PropertyCache propertyCache)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all ObservableProperties.
            query.CommandText = @"SELECT DISTINCT ?observableProperty ?valueType WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty .
                ?observableProperty rdf:type ?bNode1 .
                ?bNode1 owl:onProperty meta:hasUpperLimitValue .
                ?observableProperty rdf:type ?bNode2 .
                ?bNode2 owl:onProperty meta:hasLowerLimitValue .
                ?bNode1 owl:onDataRange ?valueType .
                ?bNode2 owl:onDataRange ?valueType . }";

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var observablePropertyNode = result["observableProperty"];
                var valueType = result["valueType"].ToString();
                valueType = valueType.Split('#')[1];

                ISensorValueHandler sensorValueHandler;
                try
                {
                    sensorValueHandler = _factory.GetSensorValueHandlerImplementation(valueType);
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

                query.SetParameter("observableProperty", observablePropertyNode);

                var innerQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);
                var rangeTuple = sensorValueHandler.FindObservablePropertyValueRange(innerQueryResult, "measuredProperty", propertyCache.ComputableProperties);
                var observableProperty = new ObservableProperty
                {
                    Name = observablePropertyNode.ToString(),
                    OwlType = valueType,
                    LowerLimitValue = rangeTuple.Item1,
                    UpperLimitValue = rangeTuple.Item2
                };

                propertyCache.ObservableProperties.Add(observablePropertyNode.ToString(), observableProperty);
            }
        }
    }
}
