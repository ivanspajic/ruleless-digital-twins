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

        public Cache Monitor() {
            _logger.LogInformation("Starting the Monitor phase.");

            var cache = new Cache {
                PropertyCache = new PropertyCache {
                    Properties = new Dictionary<string, Property>(),
                    ConfigurableParameters = new Dictionary<string, ConfigurableParameter>()
                },
                SoftSensorTreeNodes = new List<SoftSensorTreeNode>(),
                OptimalConditions = new List<OptimalCondition>()
            };

            // Get the values of all ConfigurableParameters and populate the cache.
            PopulateConfigurableParametersCache(cache.PropertyCache);

            // Get all measured Properties (Sensor Outputs) that aren't Inputs to other soft Sensors. Since soft Sensors may use
            // other Sensors' Outputs as their own Inputs, this query effectively gets the roots of the Sensor trees in the system.
            var query = @"SELECT ?property WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor ssn:implements ?procedure .
                ?procedure ssn:hasOutput ?property .
                FILTER NOT EXISTS { ?property meta:isInputOf ?otherProcedure } . }";

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // Make a dictionary to check for already made soft Sensors in the tree.
            var softSensorDictionary = new Dictionary<string, SoftSensorTreeNode>();

            // Get the values of all measured Properties (Sensor Inputs/Outputs and ConfigurableParameters) and populate the
            // cache.
            var softSensorTreeNodes = new List<SoftSensorTreeNode>();

            foreach (var result in queryResult.Results) {
                var property = result["property"];
                var softSensorTreeNode = new SoftSensorTreeNode();
                
                PopulateCacheWithPropertiesSoftSensors(property, cache.PropertyCache, softSensorTreeNode, softSensorDictionary);

                softSensorTreeNodes.Add(softSensorTreeNode);
            }

            cache.SoftSensorTreeNodes = softSensorTreeNodes;

            // Get the values of all ObservableProperties and populate the cache.
            PopulateObservablePropertiesCache(cache.PropertyCache);

            // Write Property values back to the knowledge base.
            WritePropertyValuesToKnowledgeBase(cache.PropertyCache);

            // This is necessary for the current fitness function and case-based functionality.
            cache.OptimalConditions = GetAllOptimalConditions(cache.PropertyCache);

            return cache;
        }

        private void PopulateConfigurableParametersCache(PropertyCache propertyCache) {
            var query = @"SELECT ?configurableParameter ?initialValue WHERE {
                ?configurableParameter rdf:type meta:ConfigurableParameter .
                ?configurableParameter rdf:type ?bNode .
                ?bNode rdf:type owl:Restriction .
                ?bNode owl:onProperty meta:hasValue .
                ?bNode owl:hasValue ?initialValue . }";

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            foreach (var results in queryResult.Results) {
                var propertyName = results["configurableParameter"].ToString();
                var initialValue = results["initialValue"].ToString().Split("^^")[0];
                var propertyType = _mapekKnowledge.GetPropertyType(propertyName);

                // Instantiate the new ConfigurableParameter and add it to the cache.
                var configurableParameter = new ConfigurableParameter {
                    Name = propertyName,
                    Value = initialValue,
                    OwlType = propertyType
                };
                propertyCache.ConfigurableParameters.Add(propertyName, configurableParameter);

                _logger.LogInformation("Added ConfigurableParameter {configurableParameter} to the cache.", propertyName);
            }
        }

        private void PopulateCacheWithPropertiesSoftSensors(INode propertyNode,
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

            query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?inputProperty WHERE {
                    @procedure ssn:hasInput ?inputProperty .
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
                PopulateCacheWithPropertiesSoftSensors(inputProperty, propertyCache, softSensorTreeChildNode, softSensorDictionary);

                if (propertyCache.Properties.ContainsKey(inputProperty.ToString())) {
                    inputProperties[i] = propertyCache.Properties[inputProperty.ToString()].Value;
                } else if (propertyCache.ConfigurableParameters.ContainsKey(inputProperty.ToString())) {
                    inputProperties[i] = propertyCache.ConfigurableParameters[inputProperty.ToString()].Value;
                } else {
                    throw new Exception($"The Input Property {inputProperty.ToString()} was not found in the respective Property caches.");
                }

                softSensorNodeChildren.Add(softSensorTreeChildNode);
            }

            var propertyValue = sensor.ObservePropertyValue(inputProperties);
            var property = new Property {
                Name = propertyNode.ToString(),
                OwlType = _mapekKnowledge.GetPropertyType(propertyName),
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
            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT DISTINCT ?observableProperty WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . 
                ?observableProperty rdf:type sosa:ObservableProperty . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // Get all measured Properties that are results of observing ObservableProperties.
            var innerQuery = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?outputProperty WHERE {
                    ?sensor sosa:observes @observableProperty .
                    ?sensor ssn:implements ?procedure .
                    ?procedure ssn:hasOutput ?outputProperty . }");

            foreach (var result in queryResult.Results) {
                var observablePropertyNode = result["observableProperty"];
                var observablePropertyName = observablePropertyNode.ToString();
                var valueType = _mapekKnowledge.GetPropertyType(observablePropertyName);

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

                var valueHandler = _factory.GetValueHandlerImplementation(valueType);
                var observablePropertyValue = valueHandler.GetObservablePropertyValueFromMeasuredPropertyValues(measuredPropertyValues);
                var observableProperty = new Property {
                    Name = observablePropertyName,
                    OwlType = valueType,
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

        private List<OptimalCondition> GetAllOptimalConditions(PropertyCache propertyCache) {
            var optimalConditions = new List<OptimalCondition>();

            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // For each OptimalCondition, process its respective constraints and get the appropriate Actions
            // for mitigation.
            foreach (var result in queryResult.Results) {
                var optimalConditionNode = result["optimalCondition"];
                var propertyNode = result["property"];
                var propertyName = propertyNode.ToString();

                Property property = null!;

                if (propertyCache.Properties.ContainsKey(propertyName)) {
                    property = propertyCache.Properties[propertyName];
                } else if (propertyCache.ConfigurableParameters.ContainsKey(propertyName)) {
                    property = propertyCache.ConfigurableParameters[propertyName];
                } else {
                    throw new Exception($"Property {propertyName} not found in the property cache.");
                }

                var reachedInMaximumSeconds = result["reachedInMaximumSeconds"];
                var reachedInMaximumSecondsValue = reachedInMaximumSeconds.ToString().Split('^')[0];

                // Build this OptimalCondition's full expression tree.
                var constraints = GetOptimalConditionConstraints(optimalConditionNode, propertyNode, reachedInMaximumSeconds) ??
                    throw new Exception($"OptimalCondition {optimalConditionNode.ToString()} has no constraints.");

                var optimalCondition = new OptimalCondition() {
                    Constraints = constraints,
                    ConstraintValueType = property.OwlType,
                    Name = optimalConditionNode.ToString(),
                    Property = propertyName,
                    ReachedInMaximumSeconds = int.Parse(reachedInMaximumSecondsValue),
                    UnsatisfiedAtomicConstraints = []
                };

                optimalConditions.Add(optimalCondition);
            }

            return optimalConditions;
        }

        private List<ConstraintExpression> GetOptimalConditionConstraints(INode optimalCondition, INode property, INode reachedInMaximumSeconds) {
            var constraintExpressions = new List<ConstraintExpression>();

            // This could be made more streamlined and elegant through the use of fewer, more cleverly combined queries, however,
            // SPARQL doesn't handle bNode identities, so these can't be used as variables for later referencing. For this reason, it's
            // necessary to loop through all range constraint operators (">", ">=", "<", "<=") to execute the same queries with each one.
            //
            // Documentation: (https://www.w3.org/TR/sparql11-query/#BlankNodesInResults)
            // "An application writer should not expect blank node labels in a query to refer to a particular blank node in the data."
            // For this reason, queries can be constructed with contiguous chains of bNodes, however, saving their INode objects and
            // using them as variables in subsequent queries doesn't work.
            //
            // A workaround would certainly be to insert triples as markings (much like for the inference rules), but the instance
            // model should probably not be polluted in light of other options.
            var operatorFilters = new List<ConstraintType>
            {
                ConstraintType.GreaterThan,
                ConstraintType.GreaterThanOrEqualTo,
                ConstraintType.LessThan,
                ConstraintType.LessThanOrEqualTo
            };

            AddConstraintsOfFirstOrOnlyRangeValues(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfSecondRangeValues(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfOneAndOne(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            // For models created with Protege (and the OWL API), disjunctions containing 1 and then 2 values will be converted to those
            // containing 2 and then 1.
            AddConstraintsOfDisjunctionsOfTwoAndOne(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfTwoAndTwo(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            return constraintExpressions;
        }

        private void AddConstraintsOfFirstOrOnlyRangeValues(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType in constraintTypes) {
                var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);

                // Gets the constraints of first or only values of ranges.
                var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:first [ " + operatorFilter + " ?constraint ] .}");

                query.SetParameter("optimalCondition", optimalCondition);
                query.SetParameter("property", property);
                query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var queryResult = _mapekKnowledge.ExecuteQuery(query);

                foreach (var result in queryResult.Results) {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression {
                        Right = constraint,
                        ConstraintType = constraintType
                    };

                    constraintExpressions.Add(constraintExpression);
                }
            }
        }

        private void AddConstraintsOfSecondRangeValues(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType in constraintTypes) {
                var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);

                // Gets the constraints of the second values of ranges.
                var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first [ " + operatorFilter + " ?constraint ] . }");

                query.SetParameter("optimalCondition", optimalCondition);
                query.SetParameter("property", property);
                query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var queryResult = _mapekKnowledge.ExecuteQuery(query);

                foreach (var result in queryResult.Results) {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression {
                        Right = constraint,
                        ConstraintType = constraintType
                    };

                    constraintExpressions.Add(constraintExpression);
                }
            }
        }

        private void AddConstraintsOfDisjunctionsOfOneAndOne(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType1 in constraintTypes) {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes) {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    // Gets the constraints of two disjunctive, single-valued ranges.
                    var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 WHERE {
                        @optimalCondition ssn:forProperty @property .
                        @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                        @optimalCondition rdf:type ?bNode1 .
                        ?bNode1 owl:onProperty meta:hasValueConstraint .
                        ?bNode1 owl:onDataRange ?bNode2 .
                        ?bNode2 owl:unionOf ?bNode3 .
                        ?bNode3 rdf:first ?bNode4_1 .
                        ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                        ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
                        ?bNode5_1 rdf:rest () .
                        ?bNode3 rdf:rest ?bNode4_2 .
                        ?bNode4_2 rdf:first ?bNode5_2 .
                        ?bNode5_2 owl:withRestrictions ?bNode6_2 .
                        ?bNode6_2 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
                        ?bNode6_2 rdf:rest () . }");

                    query.SetParameter("optimalCondition", optimalCondition);
                    query.SetParameter("property", property);
                    query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                    var queryResult = _mapekKnowledge.ExecuteQuery(query);

                    foreach (var result in queryResult.Results) {
                        var leftConstraint = result["constraint1"].ToString().Split('^')[0];
                        var rightConstraint = result["constraint2"].ToString().Split('^')[0];

                        var leftConstraintExpression = new AtomicConstraintExpression {
                            Right = leftConstraint,
                            ConstraintType = constraintType1
                        };
                        var rightConstraintExpression = new AtomicConstraintExpression {
                            Right = rightConstraint,
                            ConstraintType = constraintType2
                        };
                        var disjunctiveExpression = new NestedConstraintExpression {
                            Left = leftConstraintExpression,
                            Right = rightConstraintExpression,
                            ConstraintType = ConstraintType.Or
                        };

                        constraintExpressions.Add(disjunctiveExpression);
                    }
                }
            }
        }

        private void AddConstraintsOfDisjunctionsOfTwoAndOne(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType1 in constraintTypes) {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes) {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    foreach (var constraintType3 in constraintTypes) {
                        var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);

                        // Gets the constraints of two disjunctive ranges, one two-valued and the other single-valued.
                        var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 ?constraint3 WHERE {
                            @optimalCondition ssn:forProperty @property .
                            @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                            @optimalCondition rdf:type ?bNode1 .
                            ?bNode1 owl:onProperty meta:hasValueConstraint .
                            ?bNode1 owl:onDataRange ?bNode2 .
                            ?bNode2 owl:unionOf ?bNode3 .
                            ?bNode3 rdf:first ?bNode4_1 .
                            ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                            ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
                            ?bNode5_1 rdf:rest ?bNode6_1 .
                            ?bNode6_1 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
                            ?bNode3 rdf:rest ?bNode4_2 .
                            ?bNode4_2 rdf:first ?bNode5_2 .
                            ?bNode5_2 owl:withRestrictions ?bNode6_2 .
                            ?bNode6_2 rdf:first [ " + operatorFilter3 + @" ?constraint3 ] .
                            ?bNode6_2 rdf:rest () . }");

                        query.SetParameter("optimalCondition", optimalCondition);
                        query.SetParameter("property", property);
                        query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                        var queryResult = _mapekKnowledge.ExecuteQuery(query);

                        foreach (var result in queryResult.Results) {
                            var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                            var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                            var rightConstraint = result["constraint3"].ToString().Split('^')[0];

                            var leftConstraintExpression1 = new AtomicConstraintExpression {
                                Right = leftConstraint1,
                                ConstraintType = constraintType1
                            };
                            var leftConstraintExpression2 = new AtomicConstraintExpression {
                                Right = leftConstraint2,
                                ConstraintType = constraintType2
                            };
                            var rightConstraintExpression = new AtomicConstraintExpression {
                                Right = rightConstraint,
                                ConstraintType = constraintType3
                            };
                            var leftConstraintExpression = new NestedConstraintExpression {
                                Left = leftConstraintExpression1,
                                Right = leftConstraintExpression2,
                                ConstraintType = ConstraintType.And
                            };
                            var disjunctiveExpression = new NestedConstraintExpression {
                                Left = leftConstraintExpression,
                                Right = rightConstraintExpression,
                                ConstraintType = ConstraintType.Or
                            };

                            constraintExpressions.Add(disjunctiveExpression);
                        }
                    }
                }
            }
        }

        private void AddConstraintsOfDisjunctionsOfTwoAndTwo(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType1 in constraintTypes) {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes) {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    foreach (var constraintType3 in constraintTypes) {
                        var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);

                        foreach (var constraintType4 in constraintTypes) {
                            var operatorFilter4 = GetOperatorFilterFromConstraintType(constraintType4);

                            // Gets the constraints of two disjunctive, two-valued ranges.
                            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 ?constraint3 ?constraint4 WHERE {
                                @optimalCondition ssn:forProperty @property .
                                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                                @optimalCondition rdf:type ?bNode1 .
                                ?bNode1 owl:onProperty meta:hasValueConstraint .
                                ?bNode1 owl:onDataRange ?bNode2 .
                                ?bNode2 owl:unionOf ?bNode3 .
                                ?bNode3 rdf:first ?bNode4_1 .
                                ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                                ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
                                ?bNode5_1 rdf:rest ?bNode6_1 .
                                ?bNode6_1 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
                                ?bNode3 rdf:rest ?bNode4_2 .
                                ?bNode4_2 rdf:first ?bNode5_2 .
                                ?bNode5_2 owl:withRestrictions ?bNode6_2 .
                                ?bNode6_2 rdf:first [ " + operatorFilter3 + @" ?constraint3 ] .
                                ?bNode6_2 rdf:rest ?bNode7_2 .
                                ?bNode7_2 rdf:first [ " + operatorFilter4 + " ?constraint4 ] . }");

                            query.SetParameter("optimalCondition", optimalCondition);
                            query.SetParameter("property", property);
                            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                            var queryResult = _mapekKnowledge.ExecuteQuery(query);

                            foreach (var result in queryResult.Results) {
                                var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                                var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                                var rightConstraint1 = result["constraint3"].ToString().Split('^')[0];
                                var rightConstraint2 = result["constraint4"].ToString().Split('^')[0];

                                var leftConstraintExpression1 = new AtomicConstraintExpression {
                                    Right = leftConstraint1,
                                    ConstraintType = constraintType1
                                };
                                var leftConstraintExpression2 = new AtomicConstraintExpression {
                                    Right = leftConstraint2,
                                    ConstraintType = constraintType2
                                };
                                var rightConstraintExpression1 = new AtomicConstraintExpression {
                                    Right = rightConstraint1,
                                    ConstraintType = constraintType3
                                };
                                var rightConstraintExpression2 = new AtomicConstraintExpression {
                                    Right = rightConstraint2,
                                    ConstraintType = constraintType4
                                };
                                var leftConstraintExpression = new NestedConstraintExpression {
                                    Left = leftConstraintExpression1,
                                    Right = leftConstraintExpression2,
                                    ConstraintType = ConstraintType.And
                                };
                                var rightConstraintExpression = new NestedConstraintExpression {
                                    Left = rightConstraintExpression1,
                                    Right = rightConstraintExpression2,
                                    ConstraintType = ConstraintType.And
                                };
                                var disjunctiveExpression = new NestedConstraintExpression {
                                    Left = leftConstraintExpression,
                                    Right = rightConstraintExpression,
                                    ConstraintType = ConstraintType.Or
                                };

                                constraintExpressions.Add(disjunctiveExpression);
                            }
                        }
                    }
                }
            }
        }

        private static string GetOperatorFilterFromConstraintType(ConstraintType constraintType) {
            return constraintType switch {
                ConstraintType.GreaterThan => "xsd:minExclusive",
                ConstraintType.GreaterThanOrEqualTo => "xsd:minInclusive",
                ConstraintType.LessThan => "xsd:maxExclusive",
                ConstraintType.LessThanOrEqualTo => "xsd:maxInclusive",
                _ => throw new Exception($"{constraintType} is an invalid comparison operator.")
            };
        }
    }
}