using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public class MapekMonitor : IMapekMonitor
    {
        private readonly ILogger<MapekMonitor> _logger;
        private readonly IFactory _factory;

        // Used to keep copies of ConfigurableParameters and other Properties if need be. A second PropertyCache is used to allow
        // for checking of existing/non-existing Properties in the cache every MAPE-K cycle.
        private PropertyCache _oldPropertyCache;

        public MapekMonitor(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekMonitor>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();

            _oldPropertyCache = new PropertyCache
            {
                Properties = new Dictionary<string, Property>(),
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>()
            };
        }

        public PropertyCache Monitor(IGraph instanceModel)
        {
            _logger.LogInformation("Starting the Monitor phase.");

            var propertyCache = new PropertyCache
            {
                Properties = new Dictionary<string, Property>(),
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>()
            };

            // Get all measured Properties (Sensor Outputs) that aren't Inputs to other soft Sensors. Since soft Sensors may use
            // other Sensors' Outputs as their own Inputs, this query effectively gets the roots of the Sensor trees in the system.
            var query = MapekUtilities.GetParameterizedStringQuery(@"SELECT ?property WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput ?property .
                FILTER NOT EXISTS { ?property meta:isInputOf ?otherProcedure } . }");

            var queryResult = instanceModel.ExecuteQuery(query, _logger);

            // Get the values of all measured Properties (Sensor Inputs/Outputs and ConfigurableParameters) and populate the
            // cache.
            foreach (var result in queryResult.Results)
            {
                var property = result["property"];
                PopulateInputOutputsAndConfigurableParametersCaches(instanceModel, property, propertyCache);
            }

            // Get the values of all ObservableProperties and populate the cache.
            PopulateObservablePropertiesCache(instanceModel, propertyCache);

            // Keep a reference for the old cache.
            _oldPropertyCache = propertyCache;

            return propertyCache;
        }

        private void PopulateInputOutputsAndConfigurableParametersCaches(IGraph instanceModel, INode propertyNode, PropertyCache propertyCache)
        {
            var propertyName = propertyNode.ToString();

            // Simply return if the current Property already exists in the cache. This is necessary to avoid unnecessary multiple
            // executions of the same Sensors since a single Property can be an Input to multiple soft Sensors.
            if (propertyCache.Properties.ContainsKey(propertyName) || propertyCache.ConfigurableParameters.ContainsKey(propertyName))
            {
                return;
            }   

            // Get the type of the Property.
            var propertyType = MapekUtilities.GetPropertyType(_logger, instanceModel, propertyNode);

            // Get all Procedures (in Sensors) that have @property as their Output. SOSA/SSN theoretically allows for multiple Procedures
            // to have the same Output due to a lack of cardinality restrictions on the inverse predicate of 'has output' in the
            // definition of Output.
            var query = MapekUtilities.GetParameterizedStringQuery(@"SELECT ?procedure ?sensor WHERE {
                ?procedure ssn:hasOutput @property .
                ?sensor ssn:implements ?procedure .
                ?sensor rdf:type sosa:Sensor . }");

            query.SetParameter("property", propertyNode);

            var procedureQueryResult = instanceModel.ExecuteQuery(query, _logger);

            // If the current Property is not an Output of any other Procedures, then it must be a ConfigurableParameter.
            if (procedureQueryResult.IsEmpty)
            {
                AddConfigurableParameterToCache(propertyNode.ToString(), propertyType, propertyCache);
                return;
            }

            // Prepare query
            query = MapekUtilities.GetParameterizedStringQuery(@"SELECT ?inputProperty WHERE {
                    @procedure ssn:hasInput ?inputProperty .
                    @sensor ssn:implements @procedure . }");

            // Otherwise, for each Procedure, find the Inputs.
            foreach (var result in procedureQueryResult.Results)
            {
                var procedureNode = result["procedure"];
                var sensorNode = result["sensor"];
                // Get an instance of a Sensor from the factory.
                var sensor = _factory.GetSensorDeviceImplementation(sensorNode.ToString(), procedureNode.ToString());

                // Get all measured Properties this Sensor uses as its Inputs.
                query.SetParameter("procedure", procedureNode);
                query.SetParameter("sensor", sensorNode);

                var innerQueryResult = instanceModel.ExecuteQuery(query, _logger);

                // Construct the required Input Property array.
                var inputProperties = new object[innerQueryResult.Count];

                // For each Input Property, call this method recursively and record the newly-cached value in inputProperties
                // for the current Sensor to use on invocation. In case of no Inputs, the inputProperties array remains empty.
                for (var i = 0; i < innerQueryResult.Results.Count; i++)
                {
                    var inputProperty = innerQueryResult.Results[i]["inputProperty"];
                    PopulateInputOutputsAndConfigurableParametersCaches(instanceModel, inputProperty, propertyCache);

                    if (propertyCache.Properties.ContainsKey(inputProperty.ToString()))
                    {
                        inputProperties[i] = propertyCache.Properties[inputProperty.ToString()].Value;
                    }
                    else if (propertyCache.ConfigurableParameters.ContainsKey(inputProperty.ToString()))
                    {
                        inputProperties[i] = propertyCache.ConfigurableParameters[inputProperty.ToString()].Value;
                    }
                    else
                    {
                        throw new Exception($"The Input Property {inputProperty.ToString()} was not found in the respective Property caches.");
                    }
                }

                // Invoke the Sensor with the corresponding Inputs and save the returned value in the map.
                var propertyValue = sensor.ObservePropertyValue(inputProperties);
                var property = new Property
                {
                    Name = propertyNode.ToString(),
                    OwlType = propertyType,
                    Value = propertyValue
                };

                propertyCache.Properties.Add(property.Name, property);

                _logger.LogInformation("Added computable Property (Input/Output) {property} to the cache.", propertyName);
            }
        }

        private void AddConfigurableParameterToCache(string propertyName, string propertyType, PropertyCache propertyCache)
        {
            if (_oldPropertyCache.ConfigurableParameters.TryGetValue(propertyName, out ConfigurableParameter? value))
            {
                propertyCache.ConfigurableParameters.Add(propertyName, value);
                return;
            }

            var valueHandler = _factory.GetValueHandlerImplementation(propertyType);
            var initialValue = valueHandler.GetInitialValueForConfigurableParameter(propertyName);

            // Instantiate the new ConfigurableParameter and add it to the cache.
            var configurableParameter = new ConfigurableParameter
            {
                Name = propertyName,
                Value = initialValue,
                OwlType = propertyType
            };

            propertyCache.ConfigurableParameters.Add(propertyName, configurableParameter);

            _logger.LogInformation("Added ConfigurableParameter {configurableParameter} to the cache.", propertyName);
        }

        private void PopulateObservablePropertiesCache(IGraph instanceModel, PropertyCache propertyCache)
        {
            // Get all ObservableProperties.
            var query = MapekUtilities.GetParameterizedStringQuery(@"SELECT DISTINCT ?observableProperty ?valueType WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . 
                ?observableProperty rdf:type sosa:ObservableProperty . }");

            var queryResult = instanceModel.ExecuteQuery(query, _logger);
            // Get all measured Properties that are results of observing ObservableProperties.
            var innerQuery = MapekUtilities.GetParameterizedStringQuery(@"SELECT ?outputProperty WHERE {
                    ?sensor sosa:observes @observableProperty .
                    ?sensor ssn:implements ?procedure .
                    ?procedure ssn:hasOutput ?outputProperty . }");

            foreach (var result in queryResult.Results)
            {
                var observablePropertyNode = result["observableProperty"];
                var observablePropertyName = observablePropertyNode.ToString();
                var valueType = MapekUtilities.GetPropertyType(_logger, instanceModel, observablePropertyNode);

                innerQuery.SetParameter("observableProperty", observablePropertyNode);

                var innerQueryResult = instanceModel.ExecuteQuery(innerQuery, _logger);

                var measuredPropertyValues = new object[innerQueryResult.Results.Count];

                // Populate the input value array with measured Property values.
                for (var i = 0; i < measuredPropertyValues.Length; i++)
                {
                    var propertyName = innerQueryResult.Results[i]["outputProperty"].ToString();

                    if (propertyCache.Properties.TryGetValue(propertyName, out Property property))
                    {
                        measuredPropertyValues[i] = property.Value;
                    }
                    else
                    {
                        throw new Exception($"Property {propertyName} not found in property cache.");
                    }
                }

                var valueHandler = _factory.GetValueHandlerImplementation(valueType);
                var observablePropertyValue = valueHandler.GetObservablePropertyValueFromMeasuredPropertyValues(measuredPropertyValues);
                var observableProperty = new Property
                {
                    Name = observablePropertyName,
                    OwlType = valueType,
                    Value = observablePropertyValue
                };

                propertyCache.Properties.Add(observablePropertyName, observableProperty);

                _logger.LogInformation("Added ObservableProperty {observableProperty} to the cache.", observablePropertyName);
            }
        }
    }
}
