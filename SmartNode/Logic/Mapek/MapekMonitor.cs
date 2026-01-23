using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF;
using System.Diagnostics;

namespace Logic.Mapek {
    public class MapekMonitor : IMapekMonitor {
        private readonly ILogger<IMapekMonitor> _logger;
        private readonly IFactory _factory;
        private readonly IMapekKnowledge _mapekKnowledge;

        public MapekMonitor(IServiceProvider serviceProvider) {
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekMonitor>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
            _mapekKnowledge = serviceProvider.GetRequiredService<IMapekKnowledge>();
        }

        public async Task<Cache> Monitor() {
            _logger.LogInformation("Starting the Monitor phase.");

            var cache = new Cache {
                PropertyCache = new PropertyCache {
                    Properties = new Dictionary<string, Property>(),
                    ConfigurableParameters = new Dictionary<string, ConfigurableParameter>()
                },
                SoftSensorTreeNodes = new List<SoftSensorTreeNode>(),
                Conditions = new List<Condition>()
            };

            // Get the values of all ConfigurableParameters and populate the cache.
            PopulateConfigurableParametersCache(cache.PropertyCache);

            // Get all measured Properties (Sensor Outputs) that aren't Inputs to other soft Sensors. Since soft Sensors may use
            // other Sensors' Outputs as their own Inputs, this query effectively gets the roots of the Sensor trees in the system.
            var query = @"SELECT ?property ?initialValue WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput ?property .
                ?property meta:hasValue ?initialValue .
                FILTER NOT EXISTS { ?property meta:isInputOf ?otherProcedure } . }";

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // Make a dictionary to check for already made soft Sensors in the tree.
            var softSensorDictionary = new Dictionary<string, SoftSensorTreeNode>();

            // Get the values of all measured Properties (Sensor Inputs/Outputs and ConfigurableParameters) and populate the cache.
            var softSensorTreeNodes = new List<SoftSensorTreeNode>();

            foreach (var result in queryResult.Results) {
                var property = result["property"];
                var valueType = result["initialValue"].ToString().Split("^^")[1];

                var softSensorTreeNode = new SoftSensorTreeNode();
                
                await PopulateCacheWithPropertiesSoftSensors(property, valueType, cache.PropertyCache, softSensorTreeNode, softSensorDictionary);

                softSensorTreeNodes.Add(softSensorTreeNode);
            }

            cache.SoftSensorTreeNodes = softSensorTreeNodes;

            // Get the values of all ObservableProperties and populate the cache.
            PopulateObservablePropertiesCache(cache.PropertyCache);

            // Write Property values back to the knowledge base.
            WritePropertyValuesToKnowledgeBase(cache.PropertyCache);

            // This is necessary for the current fitness function and case-based functionality.
            cache.Conditions = _mapekKnowledge.GetAllConditions(cache.PropertyCache);

            return cache;
        }

        private void PopulateConfigurableParametersCache(PropertyCache propertyCache) {
            var query = @"SELECT ?configurableParameter ?initialValue WHERE {
                ?configurableParameter rdf:type meta:ConfigurableParameter .
                ?configurableParameter meta:hasValue ?initialValue . }";

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            foreach (var results in queryResult.Results) {
                var propertyName = results["configurableParameter"].ToString();
                var initialValueTypeSplit = results["initialValue"].ToString().Split("^^");

                // Instantiate the new ConfigurableParameter and add it to the cache.
                var configurableParameter = new ConfigurableParameter {
                    Name = propertyName,
                    Value = initialValueTypeSplit[0],
                    OwlType = initialValueTypeSplit[1]
                };
                propertyCache.ConfigurableParameters.Add(propertyName, configurableParameter);

                _logger.LogInformation("Added ConfigurableParameter {configurableParameter} to the cache.", propertyName);
            }
        }

        private async Task PopulateCacheWithPropertiesSoftSensors(INode propertyNode,
            string propertyType,
            PropertyCache propertyCache,
            SoftSensorTreeNode softSensorTreeNode,
            Dictionary<string, SoftSensorTreeNode> softSensorDictionary) {
            var propertyName = propertyNode.ToString();

            // Simply return if the current Property already exists in the cache. This is necessary to avoid unnecessary multiple
            // executions of the same Sensors since a single Property can be an Input to multiple soft Sensors. This also means a Sensor
            // for that Property is also in the cache.
            if (propertyCache.Properties.ContainsKey(propertyName) || propertyCache.ConfigurableParameters.ContainsKey(propertyName)) {
                if (softSensorDictionary.TryGetValue(propertyName, out SoftSensorTreeNode? existingSoftSensorTreeNode)) {
                    softSensorTreeNode.OutputProperty = existingSoftSensorTreeNode.OutputProperty;
                    softSensorTreeNode.Children = existingSoftSensorTreeNode.Children;
                    softSensorTreeNode.NodeItem = existingSoftSensorTreeNode.NodeItem;

                    return;
                }

                // Shouldn't happen, but in case it does... :)
                throw new Exception($"No sensor registered for Output Property {propertyName}.");
            }

            var softSensorNodeChildren = new List<SoftSensorTreeNode>();

            // Get all Procedures (in Sensors) that have @property as their Output. SOSA/SSN theoretically allows for multiple Procedures
            // to have the same Output due to a lack of cardinality restrictions on the inverse predicate of 'has output' in the
            // definition of Output.
            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?procedure ?sensor WHERE {
                ?procedure ssn:hasOutput @property .
                ?sensor ssn:implements ?procedure .
                ?sensor rdf:type sosa:Sensor . }");

            query.SetParameter("property", propertyNode);

            var procedureQueryResult = _mapekKnowledge.ExecuteQuery(query);

            // Although there may be multiple results here, the instance models should be configured such that a single soft Sensor
            // (Procedure) outputs a unique Property. This avoids potential cases of multiple values for the same Property.
            var procedureNode = procedureQueryResult.Results[0]["procedure"];
            var sensorNode = procedureQueryResult.Results[0]["sensor"];

            // Get an instance of a Sensor from the factory.
            var sensor = _factory.GetSensorImplementation(sensorNode.ToString(), procedureNode.ToString());

            query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?inputProperty ?initialValue WHERE {
                    @procedure ssn:hasInput ?inputProperty .
                    ?inputProperty meta:hasValue ?initialValue .
                    @sensor ssn:implements @procedure .
                    FILTER NOT EXISTS { ?inputProperty rdf:type meta:ConfigurableParameter . } }");

            // Get all measured Properties this Sensor uses as its Inputs.
            query.SetParameter("procedure", procedureNode);
            query.SetParameter("sensor", sensorNode);

            var innerQueryResult = _mapekKnowledge.ExecuteQuery(query);

            // Construct the required Input Property array.
            var inputProperties = new object[innerQueryResult.Count];

            // For each Input Property, call this method recursively and record the newly-cached value in inputProperties
            // for the current Sensor to use on invocation. In case of no Inputs, the inputProperties array remains empty.
            for (var i = 0; i < innerQueryResult.Results.Count; i++) {
                var softSensorTreeChildNode = new SoftSensorTreeNode();

                var inputProperty = innerQueryResult.Results[i]["inputProperty"];
                var valueType = innerQueryResult.Results[i]["initialValue"].ToString().Split("^^")[1];
                await PopulateCacheWithPropertiesSoftSensors(inputProperty, valueType, propertyCache, softSensorTreeChildNode, softSensorDictionary);

                if (propertyCache.Properties.ContainsKey(inputProperty.ToString())) {
                    inputProperties[i] = propertyCache.Properties[inputProperty.ToString()].Value;
                } else if (propertyCache.ConfigurableParameters.ContainsKey(inputProperty.ToString())) {
                    inputProperties[i] = propertyCache.ConfigurableParameters[inputProperty.ToString()].Value;
                } else {
                    throw new Exception($"The Input Property {inputProperty.ToString()} was not found in the respective Property caches.");
                }

                softSensorNodeChildren.Add(softSensorTreeChildNode);
            }

            var propertyValue = await sensor.ObservePropertyValue(inputProperties);
            var property = new Property {
                Name = propertyName,
                OwlType = propertyType,
                Value = propertyValue
            };

            propertyCache.Properties.Add(property.Name, property);

            // Build the soft sensor node with any potential children.
            softSensorTreeNode.NodeItem = sensor;
            softSensorTreeNode.Children = softSensorNodeChildren;
            softSensorTreeNode.OutputProperty = property.Name;

            // Cache it for easier (and faster) finding.
            softSensorDictionary.Add(property.Name, softSensorTreeNode);

            _logger.LogInformation("Added computable Property (Input/Output) {property} to the cache.", propertyName);
        }

        private void PopulateObservablePropertiesCache(PropertyCache propertyCache) {
            // Get all ObservableProperties.
            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT DISTINCT ?observableProperty ?initialValue WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . 
                ?observableProperty rdf:type sosa:ObservableProperty .
                ?observableProperty meta:hasValue ?initialValue . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // Get all measured Properties that are results of observing ObservableProperties.
            var innerQuery = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?outputProperty WHERE {
                    ?sensor sosa:observes @observableProperty .
                    ?sensor ssn:implements ?procedure .
                    ?procedure ssn:hasOutput ?outputProperty . }");

            foreach (var result in queryResult.Results) {
                var observablePropertyNode = result["observableProperty"];
                var observablePropertyName = observablePropertyNode.ToString();
                var observablePropertyType = result["initialValue"].ToString().Split("^^")[1];

                innerQuery.SetParameter("observableProperty", observablePropertyNode);

                var innerQueryResult = _mapekKnowledge.ExecuteQuery(innerQuery);

                var measuredPropertyValues = new object[innerQueryResult.Results.Count];
                // XXX WF
                Debug.Assert(innerQueryResult.Results.Count > 0, $"ObservableProperty {result["sensor"].ToString()}/{observablePropertyName} without measured Properties.");

                // Populate the input value array with measured Property values.
                for (var i = 0; i < measuredPropertyValues.Length; i++) {
                    var propertyName = innerQueryResult.Results[i]["outputProperty"].ToString();

                    if (propertyCache.Properties.TryGetValue(propertyName, out Property? property)) {
                        measuredPropertyValues[i] = property.Value;
                    } else {
                        throw new Exception($"Property {propertyName} not found in property cache.");
                    }
                }

                var valueHandler = _factory.GetValueHandlerImplementation(observablePropertyType);
                var observablePropertyValue = valueHandler.GetObservablePropertyValueFromMeasuredPropertyValues(measuredPropertyValues);
                var observableProperty = new Property {
                    Name = observablePropertyName,
                    OwlType = observablePropertyType,
                    Value = observablePropertyValue
                };

                propertyCache.Properties.Add(observablePropertyName, observableProperty);

                _logger.LogInformation("Added ObservableProperty {observableProperty} to the cache.", observablePropertyName);
            }
        }

        private void WritePropertyValuesToKnowledgeBase(PropertyCache propertyCache) {
            foreach (var property in propertyCache.Properties.Values) {
                _mapekKnowledge.UpdatePropertyValue(property);
            }

            foreach (var property in propertyCache.ConfigurableParameters.Values) {
                _mapekKnowledge.UpdatePropertyValue(property);
            }

            _mapekKnowledge.CommitInMemoryInstanceModelToKnowledgeBase();
        }
    }
}