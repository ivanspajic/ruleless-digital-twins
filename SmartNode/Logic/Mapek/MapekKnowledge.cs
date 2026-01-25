using System.Diagnostics;
using Logic.FactoryInterface;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Update;
using VDS.RDF.Writing;

namespace Logic.Mapek {
    public class MapekKnowledge : IMapekKnowledge {
        public const string DtPrefix = "meta";
        public const string DtUri = "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/";
        public const string SosaPrefix = "sosa";
        public const string SosaUri = "http://www.w3.org/ns/sosa/";
        public const string SsnPrefix = "ssn";
        public const string SsnUri = "http://www.w3.org/ns/ssn/";
        public const string RdfPrefix = "rdf";
        public const string RdfUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public const string OwlPrefix = "owl";
        public const string OwlUri = "http://www.w3.org/2002/07/owl#";
        public const string XsdPrefix = "xsd";
        public const string XsdUri = "http://www.w3.org/2001/XMLSchema#";

        private readonly ILogger<IMapekKnowledge> _logger;
        private readonly IFactory _factory;
        private readonly FilepathArguments _filepathArguments;

        private readonly Graph _instanceModel;
        private readonly Graph _inferredModel;
        private readonly TurtleParser _turtleParser;
        private readonly CompressingTurtleWriter _turtleWriter;

        public MapekKnowledge(IServiceProvider serviceProvider) {
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekKnowledge>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
            _filepathArguments = serviceProvider.GetRequiredService<FilepathArguments>();
            _turtleWriter = new CompressingTurtleWriter();

            _instanceModel = new Graph();
            _inferredModel = new Graph();
            _turtleParser = new TurtleParser();
            LoadModelsFromKnowledgeBase();
            
            // If nothing was loaded, don't start the loop.
            if (_instanceModel.IsEmpty) {
                throw new Exception("There is nothing in the instance model graph.");
            }
        }

        public SparqlParameterizedString GetParameterizedStringQuery(string queryString) {
            var query = new SparqlParameterizedString {
                CommandText = queryString
            };

            // Register the relevant prefixes for the queries to come.
            query.Namespaces.AddNamespace(DtPrefix, new Uri(DtUri));
            query.Namespaces.AddNamespace(SosaPrefix, new Uri(SosaUri));
            query.Namespaces.AddNamespace(SsnPrefix, new Uri(SsnUri));
            query.Namespaces.AddNamespace(RdfPrefix, new Uri(RdfUri));
            query.Namespaces.AddNamespace(OwlPrefix, new Uri(OwlUri));
            query.Namespaces.AddNamespace(XsdPrefix, new Uri(XsdUri));

            return query;
        }

        public SparqlResultSet ExecuteQuery(string queryString) {
            var query = GetParameterizedStringQuery(queryString);

            return ExecuteQuery(query);
        }

        bool suppressLogging = false;
        // Suppress logging in selected places. An alternative would be to switch to log level DEBUG instead INFO.
        private SparqlResultSet ExecuteSuppressedQuery(SparqlParameterizedString query) {
            suppressLogging = true;
            var result = ExecuteQuery(query);
            suppressLogging = false;
            return result;
        }

        public SparqlResultSet ExecuteQuery(SparqlParameterizedString query, bool useInferredModel = false) {
            SparqlResultSet queryResult;
            
            if (useInferredModel) {
                queryResult = (SparqlResultSet)_inferredModel.ExecuteQuery(query);
            } else {
                queryResult = (SparqlResultSet)_instanceModel.ExecuteQuery(query);
            }

            // Some parts like finding optimal conditions really spam the log, so introduce an override:
            if (!suppressLogging) {
                _logger.LogInformation("Executed query: {query} ({numResults})", query.CommandText, queryResult.Results.Count);

                if (!queryResult.IsEmpty) {
                    var resultString = string.Join("\n", queryResult.Results.Select(r => r.ToString()));
                    _logger.LogInformation("Query result: {resultString}", resultString);
                }
            }

            return queryResult;
        }

        public void UpdatePropertyValue(Property property) {
            var valueHandler = _factory.GetValueHandlerImplementation(property.OwlType);
            var propertyValue = valueHandler.GetValueAsCultureInvariantString(property.Value);

            // Update ObservableProperties first.
            var query = GetParameterizedStringQuery(@"DELETE {
                    ?bNode owl:hasValue ?oldValue .
                }
                INSERT {
                    ?bNode owl:hasValue @newValue^^@type .
                }
                WHERE {
                    @property rdf:type sosa:ObservableProperty .
                    @property rdf:type ?bNode .
                    ?bNode owl:hasValue ?oldValue .
                }");

            query.SetLiteral("newValue", propertyValue, false);
            query.SetUri("type", new Uri(property.OwlType));
            query.SetUri("property", new Uri(property.Name));

            UpdateModel(query);

            // In case there was no match on an ObservableProperty, try to update a matching Output.
            query = GetParameterizedStringQuery(@"DELETE {
                    ?bNode owl:hasValue ?oldValue .
                }
                INSERT {
                    ?bNode owl:hasValue @newValue^^@type .
                }
                WHERE {
                    @property rdf:type ssn:Property .
                    @property rdf:type ssn:Output .
                    @property rdf:type ?bNode .
                    ?bNode owl:hasValue ?oldValue .
                }");

            query.SetLiteral("newValue", propertyValue, false);
            query.SetUri("type", new Uri(property.OwlType));
            query.SetUri("property", new Uri(property.Name));

            UpdateModel(query);
        }

        public void UpdateConfigurableParameterValue(ConfigurableParameter configurableParameter) {
            var query = GetParameterizedStringQuery(@"DELETE {
                    ?bNode owl:hasValue ?oldValue .
                }
                INSERT {
                    ?bNode owl:hasValue @newValue^^@type .
                }
                WHERE {
                    @configurableParameter rdf:type meta:ConfigurableParameter .
                    @configurableParameter rdf:type ?bNode .
                    ?bNode owl:hasValue ?oldValue .
                }");

            query.SetLiteral("newValue", configurableParameter.Value.ToString(), false);
            query.SetUri("type", new Uri(configurableParameter.OwlType));
            query.SetUri("configurableParameter", new Uri(configurableParameter.Name));

            UpdateModel(query);
        }

        public void CommitInMemoryInstanceModelToKnowledgeBase() {
            _turtleWriter.Save(_instanceModel, _filepathArguments.InstanceModelFilepath);
        }

        public void LoadModelsFromKnowledgeBase() {
            _instanceModel.Clear();
            _inferredModel.Clear();

            _turtleParser.Load(_instanceModel, _filepathArguments.InstanceModelFilepath);
            _turtleParser.Load(_inferredModel, _filepathArguments.InferredModelFilepath);
        }

        public void UpdateModel(SparqlParameterizedString query) {
            var sparqlUpdateParser = new SparqlUpdateParser();
            var inMemoryDataset = new InMemoryDataset(_instanceModel);
            var processor = new LeviathanUpdateProcessor(inMemoryDataset);
            var commandSet = sparqlUpdateParser.ParseFromString(query);

            processor.ProcessCommandSet(commandSet);
        }

        internal void Validate(PropertyCache? pc)
        {
            // Check every Actuator has at least a single state?
            // Check every Observable from a Sensor is in the cache
            // TODO: not actually parametrized...
            // TODO: provide list of diagnostics instead of stopping on the first one.
            var query = GetParameterizedStringQuery(@"SELECT ?sensor ?property WHERE {
                ?sensor rdf:type sosa:Sensor .
                OPTIONAL { ?sensor sosa:observes ?property } .
                }");
            var result = ExecuteQuery(query);
            foreach(var r in result) {
                if (!r.TryGetValue("property", out var p)) {
                    Debug.Fail($"Sensor without Property: {r["sensor"].ToString()}");
                } else {
                    if (!pc!.Properties.ContainsKey(p.ToString())) {
                        Debug.Fail($"PropertyCache without value for Property: {p.ToString()}");
                    }
                }
            }
        }

        public IEnumerable<Condition> GetAllConditions(PropertyCache propertyCache) {
            var conditions = new List<Condition>();

            var conditionQuery = GetParameterizedStringQuery(@"SELECT ?condition ?property ?isBreakable ?priority ?reachedInMaximumSeconds ?satisfiedBy WHERE {
                ?condition rdf:type meta:Condition .
                ?condition ssn:forProperty ?property .
                ?condition meta:isBreakable ?isBreakable .
                OPTIONAL {
                    ?condition meta:hasPriority ?priority .
                }
                OPTIONAL {
                    ?condition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds .
                }
                OPTIONAL {
                    ?condition meta:satisfiedBy ?satisfiedBy .
                }}");

            var conditionQueryResult = ExecuteQuery(conditionQuery);

            foreach (var result in conditionQueryResult.Results) {
                var conditionNode = result["condition"];
                var propertyName = result["property"].ToString();
                var isBreakable = bool.Parse(result["isBreakable"].ToString().Split("^^")[0]);
                var priorityString = GetOptionalQueryResult(result, "priority");
                var reachedInMaximumSecondsString = GetOptionalQueryResult(result, "reachedInMaximumSeconds");
                var satisfiedByString = GetOptionalQueryResult(result, "satisfiedBy");

                var property = MapekUtilities.GetPropertyFromPropertyCacheByName(propertyCache, propertyName);

                var constraints = GetConditionConstraints(conditionNode, propertyCache);

                var condition = new Condition {
                    Constraints = constraints,
                    Name = conditionNode.ToString(),
                    Property = property,
                    IsBreakable = isBreakable,
                    Priority = priorityString is not null ? int.Parse(priorityString) : null!,
                    ReachedInMaximumSeconds = reachedInMaximumSecondsString is not null ? int.Parse(reachedInMaximumSecondsString) : null!,
                    SatisfiedBy = satisfiedByString is not null ? DateTime.Parse(satisfiedByString) : null!
                };

                conditions.Add(condition);
            }

            return conditions;
        }

        private static string GetOptionalQueryResult(ISparqlResult queryResult, string variableName) {
            return queryResult.HasValue(variableName) ? queryResult[variableName].ToString().Split("^^")[0] : null!;
        }

        private List<ConstraintExpression> GetConditionConstraints(INode condition, PropertyCache propertyCache) {
            var constraintExpressions = new List<ConstraintExpression>();

            // This could be made more streamlined and elegant through the use of fewer, more cleverly combined queries, however,
            // SPARQL doesn't handle bNode identities, so these can't be used as variables for later referencing.
            //
            // Documentation: (https://www.w3.org/TR/sparql11-query/#BlankNodesInResults)
            // "An application writer should not expect blank node labels in a query to refer to a particular blank node in the data."
            // For this reason, queries can be constructed with continuous chains of bNodes, however, saving their INode objects and
            // using them as variables in subsequent queries doesn't work.
            //
            // A workaround would certainly be to insert triples as markings (much like for the inference rules), but the instance
            // model should probably not be (more) polluted in light of other options.

            var comparisonOperators = new List<ConstraintType> {
                ConstraintType.EqualTo,
                ConstraintType.GreaterThan,
                ConstraintType.GreaterThanOrEqualTo,
                ConstraintType.LessThan,
                ConstraintType.LessThanOrEqualTo
            };

            // Example: >15
            AddSingleConstraints(constraintExpressions, condition, comparisonOperators, propertyCache);
            // Example: >15 and <20
            AddConjunctiveConstraints(constraintExpressions, condition);
            // Example: <15 or >20
            AddDisjunctiveSingleSingleConstraints(constraintExpressions, condition);
            // Example: (>15 and <20) or >25
            AddDisjunctiveDoubleSingleConstraints(constraintExpressions, condition);
            // Example: (>15 and <20) or (>25 and <30)
            AddDisjunctiveDoubleDoubleConstraints(constraintExpressions, condition);

            return constraintExpressions;
        }

        private void AddSingleConstraints(IList<ConstraintExpression> constraintExpressions, INode conditionNode, IEnumerable<ConstraintType> comparisonOperators, PropertyCache propertyCache) {
            foreach (var comparisonOperator in comparisonOperators) {
                var operatorObjectProperty = GetOperatorObjectPropertyFromConstraintType(comparisonOperator);

                var query = GetParameterizedStringQuery(@"SELECT ?boundProperty ?operator WHERE {
                    @condition rdf:type ?aNode .
                    ?aNode rdf:type owl:Restriction .
                    ?aNode owl:hasValue ?boundProperty .
                    ?aNode owl:onProperty " + operatorObjectProperty + " .}");

                query.SetParameter("condition", conditionNode);

                var queryResult = ExecuteSuppressedQuery(query);

                foreach (var result in queryResult.Results) {
                    var boundPropertyName = result["boundProperty"].ToString();
                    var boundProperty = MapekUtilities.GetPropertyFromPropertyCacheByName(propertyCache, boundPropertyName);

                    constraintExpressions.Add(new AtomicConstraintExpression {
                        ConstraintType = comparisonOperator,
                        Property = boundProperty
                    });
                }
            }
        }

        private void AddConjunctiveConstraints(IList<ConstraintExpression> constraintExpressions, INode conditionNode) {

        }

        private void AddDisjunctiveSingleSingleConstraints(IList<ConstraintExpression> constraintExpressions, INode conditionNode) {

        }

        private void AddDisjunctiveDoubleSingleConstraints(IList<ConstraintExpression> constraintExpressions, INode conditionNode) {

        }

        private void AddDisjunctiveDoubleDoubleConstraints(IList<ConstraintExpression> constraintExpressions, INode conditionNode) {

        }

        //private void AddConstraintsOfFirstOrOnlyRangeValues(IList<ConstraintExpression> constraintExpressions,
        //    INode condition,
        //    IEnumerable<ConstraintType> constraintTypes) {
        //    foreach (var constraintType in constraintTypes) {
        //        var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);

        //        // Gets the constraints of first or only values of ranges.
        //        var query = GetParameterizedStringQuery(@"SELECT ?constraint WHERE {
        //            @condition ssn:forProperty @property .
        //            @condition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
        //            @condition rdf:type ?bNode1 .
        //            ?bNode1 owl:onProperty meta:hasValueConstraint .
        //            ?bNode1 owl:onDataRange ?bNode2 .
        //            ?bNode2 owl:withRestrictions ?bNode3 .
        //            ?bNode3 rdf:first [ " + operatorFilter + " ?constraint ] .}");

        //        query.SetParameter("condition", condition);

        //        var queryResult = ExecuteSuppressedQuery(query);

        //        foreach (var result in queryResult.Results) {
        //            var constraint = result["constraint"].ToString().Split('^')[0];

        //            var constraintExpression = new AtomicConstraintExpression {
        //                Property = constraint,
        //                ConstraintType = constraintType
        //            };

        //            constraintExpressions.Add(constraintExpression);
        //        }
        //    }
        //}

        //private void AddConstraintsOfSecondRangeValues(IList<ConstraintExpression> constraintExpressions,
        //    INode condition,
        //    IEnumerable<ConstraintType> constraintTypes) {
        //    foreach (var constraintType in constraintTypes) {
        //        var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);

        //        // Gets the constraints of the second values of ranges.
        //        var query = GetParameterizedStringQuery(@"SELECT ?constraint WHERE {
        //            @condition ssn:forProperty @property .
        //            @condition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
        //            @condition rdf:type ?bNode1 .
        //            ?bNode1 owl:onProperty meta:hasValueConstraint .
        //            ?bNode1 owl:onDataRange ?bNode2 .
        //            ?bNode2 owl:withRestrictions ?bNode3 .
        //            ?bNode3 rdf:rest ?bNode4 .
        //            ?bNode4 rdf:first [ " + operatorFilter + " ?constraint ] . }");

        //        query.SetParameter("condition", condition);

        //        var queryResult = ExecuteSuppressedQuery(query);

        //        foreach (var result in queryResult.Results) {
        //            var constraint = result["constraint"].ToString().Split('^')[0];

        //            var constraintExpression = new AtomicConstraintExpression {
        //                Property = constraint,
        //                ConstraintType = constraintType
        //            };

        //            constraintExpressions.Add(constraintExpression);
        //        }
        //    }
        //}

        //private void AddConstraintsOfDisjunctionsOfOneAndOne(IList<ConstraintExpression> constraintExpressions,
        //    INode condition,
        //    INode property,
        //    INode reachedInMaximumSeconds,
        //    IEnumerable<ConstraintType> constraintTypes) {
        //    foreach (var constraintType1 in constraintTypes) {
        //        var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

        //        foreach (var constraintType2 in constraintTypes) {
        //            var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

        //            // Gets the constraints of two disjunctive, single-valued ranges.
        //            var query = GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 WHERE {
        //                @condition ssn:forProperty @property .
        //                @condition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
        //                @condition rdf:type ?bNode1 .
        //                ?bNode1 owl:onProperty meta:hasValueConstraint .
        //                ?bNode1 owl:onDataRange ?bNode2 .
        //                ?bNode2 owl:unionOf ?bNode3 .
        //                ?bNode3 rdf:first ?bNode4_1 .
        //                ?bNode4_1 owl:withRestrictions ?bNode5_1 .
        //                ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
        //                ?bNode5_1 rdf:rest () .
        //                ?bNode3 rdf:rest ?bNode4_2 .
        //                ?bNode4_2 rdf:first ?bNode5_2 .
        //                ?bNode5_2 owl:withRestrictions ?bNode6_2 .
        //                ?bNode6_2 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
        //                ?bNode6_2 rdf:rest () . }");

        //            query.SetParameter("condition", condition);
        //            query.SetParameter("property", property);
        //            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

        //            var queryResult = ExecuteSuppressedQuery(query);

        //            foreach (var result in queryResult.Results) {
        //                var leftConstraint = result["constraint1"].ToString().Split('^')[0];
        //                var rightConstraint = result["constraint2"].ToString().Split('^')[0];

        //                var leftConstraintExpression = new AtomicConstraintExpression {
        //                    Property = leftConstraint,
        //                    ConstraintType = constraintType1
        //                };
        //                var rightConstraintExpression = new AtomicConstraintExpression {
        //                    Property = rightConstraint,
        //                    ConstraintType = constraintType2
        //                };
        //                var disjunctiveExpression = new NestedConstraintExpression {
        //                    Left = leftConstraintExpression,
        //                    Right = rightConstraintExpression,
        //                    ConstraintType = ConstraintType.Or
        //                };

        //                constraintExpressions.Add(disjunctiveExpression);
        //            }
        //        }
        //    }
        //}

        //private void AddConstraintsOfDisjunctionsOfTwoAndOne(IList<ConstraintExpression> constraintExpressions,
        //    INode condition,
        //    INode property,
        //    INode reachedInMaximumSeconds,
        //    IEnumerable<ConstraintType> constraintTypes) {
        //    foreach (var constraintType1 in constraintTypes) {
        //        var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

        //        foreach (var constraintType2 in constraintTypes) {
        //            var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

        //            foreach (var constraintType3 in constraintTypes) {
        //                var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);

        //                // Gets the constraints of two disjunctive ranges, one two-valued and the other single-valued.
        //                var query = GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 ?constraint3 WHERE {
        //                    @condition ssn:forProperty @property .
        //                    @condition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
        //                    @condition rdf:type ?bNode1 .
        //                    ?bNode1 owl:onProperty meta:hasValueConstraint .
        //                    ?bNode1 owl:onDataRange ?bNode2 .
        //                    ?bNode2 owl:unionOf ?bNode3 .
        //                    ?bNode3 rdf:first ?bNode4_1 .
        //                    ?bNode4_1 owl:withRestrictions ?bNode5_1 .
        //                    ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
        //                    ?bNode5_1 rdf:rest ?bNode6_1 .
        //                    ?bNode6_1 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
        //                    ?bNode3 rdf:rest ?bNode4_2 .
        //                    ?bNode4_2 rdf:first ?bNode5_2 .
        //                    ?bNode5_2 owl:withRestrictions ?bNode6_2 .
        //                    ?bNode6_2 rdf:first [ " + operatorFilter3 + @" ?constraint3 ] .
        //                    ?bNode6_2 rdf:rest () . }");

        //                query.SetParameter("condition", condition);
        //                query.SetParameter("property", property);
        //                query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

        //                var queryResult = ExecuteSuppressedQuery(query);

        //                foreach (var result in queryResult.Results) {
        //                    var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
        //                    var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
        //                    var rightConstraint = result["constraint3"].ToString().Split('^')[0];

        //                    var leftConstraintExpression1 = new AtomicConstraintExpression {
        //                        Property = leftConstraint1,
        //                        ConstraintType = constraintType1
        //                    };
        //                    var leftConstraintExpression2 = new AtomicConstraintExpression {
        //                        Property = leftConstraint2,
        //                        ConstraintType = constraintType2
        //                    };
        //                    var rightConstraintExpression = new AtomicConstraintExpression {
        //                        Property = rightConstraint,
        //                        ConstraintType = constraintType3
        //                    };
        //                    var leftConstraintExpression = new NestedConstraintExpression {
        //                        Left = leftConstraintExpression1,
        //                        Right = leftConstraintExpression2,
        //                        ConstraintType = ConstraintType.And
        //                    };
        //                    var disjunctiveExpression = new NestedConstraintExpression {
        //                        Left = leftConstraintExpression,
        //                        Right = rightConstraintExpression,
        //                        ConstraintType = ConstraintType.Or
        //                    };

        //                    constraintExpressions.Add(disjunctiveExpression);
        //                }
        //            }
        //        }
        //    }
        //}

        //private void AddConstraintsOfDisjunctionsOfTwoAndTwo(IList<ConstraintExpression> constraintExpressions,
        //    INode condition,
        //    INode property,
        //    INode reachedInMaximumSeconds,
        //    IEnumerable<ConstraintType> constraintTypes) {
        //    foreach (var constraintType1 in constraintTypes) {
        //        var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

        //        foreach (var constraintType2 in constraintTypes) {
        //            var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

        //            foreach (var constraintType3 in constraintTypes) {
        //                var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);

        //                foreach (var constraintType4 in constraintTypes) {
        //                    var operatorFilter4 = GetOperatorFilterFromConstraintType(constraintType4);

        //                    // Gets the constraints of two disjunctive, two-valued ranges.
        //                    var query = GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 ?constraint3 ?constraint4 WHERE {
        //                        @condition ssn:forProperty @property .
        //                        @condition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
        //                        @condition rdf:type ?bNode1 .
        //                        ?bNode1 owl:onProperty meta:hasValueConstraint .
        //                        ?bNode1 owl:onDataRange ?bNode2 .
        //                        ?bNode2 owl:unionOf ?bNode3 .
        //                        ?bNode3 rdf:first ?bNode4_1 .
        //                        ?bNode4_1 owl:withRestrictions ?bNode5_1 .
        //                        ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
        //                        ?bNode5_1 rdf:rest ?bNode6_1 .
        //                        ?bNode6_1 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
        //                        ?bNode3 rdf:rest ?bNode4_2 .
        //                        ?bNode4_2 rdf:first ?bNode5_2 .
        //                        ?bNode5_2 owl:withRestrictions ?bNode6_2 .
        //                        ?bNode6_2 rdf:first [ " + operatorFilter3 + @" ?constraint3 ] .
        //                        ?bNode6_2 rdf:rest ?bNode7_2 .
        //                        ?bNode7_2 rdf:first [ " + operatorFilter4 + " ?constraint4 ] . }");

        //                    query.SetParameter("condition", condition);
        //                    query.SetParameter("property", property);
        //                    query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

        //                    var queryResult = ExecuteSuppressedQuery(query);

        //                    foreach (var result in queryResult.Results) {
        //                        var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
        //                        var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
        //                        var rightConstraint1 = result["constraint3"].ToString().Split('^')[0];
        //                        var rightConstraint2 = result["constraint4"].ToString().Split('^')[0];

        //                        var leftConstraintExpression1 = new AtomicConstraintExpression {
        //                            Property = leftConstraint1,
        //                            ConstraintType = constraintType1
        //                        };
        //                        var leftConstraintExpression2 = new AtomicConstraintExpression {
        //                            Property = leftConstraint2,
        //                            ConstraintType = constraintType2
        //                        };
        //                        var rightConstraintExpression1 = new AtomicConstraintExpression {
        //                            Property = rightConstraint1,
        //                            ConstraintType = constraintType3
        //                        };
        //                        var rightConstraintExpression2 = new AtomicConstraintExpression {
        //                            Property = rightConstraint2,
        //                            ConstraintType = constraintType4
        //                        };
        //                        var leftConstraintExpression = new NestedConstraintExpression {
        //                            Left = leftConstraintExpression1,
        //                            Right = leftConstraintExpression2,
        //                            ConstraintType = ConstraintType.And
        //                        };
        //                        var rightConstraintExpression = new NestedConstraintExpression {
        //                            Left = rightConstraintExpression1,
        //                            Right = rightConstraintExpression2,
        //                            ConstraintType = ConstraintType.And
        //                        };
        //                        var disjunctiveExpression = new NestedConstraintExpression {
        //                            Left = leftConstraintExpression,
        //                            Right = rightConstraintExpression,
        //                            ConstraintType = ConstraintType.Or
        //                        };

        //                        constraintExpressions.Add(disjunctiveExpression);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        private static string GetOperatorObjectPropertyFromConstraintType(ConstraintType constraintType) {
            return constraintType switch {
                ConstraintType.EqualTo => "meta:equalTo",
                ConstraintType.GreaterThan => "meta:greaterThan",
                ConstraintType.GreaterThanOrEqualTo => "meta:greaterThanOrEqualTo",
                ConstraintType.LessThan => "meta:lessThan",
                ConstraintType.LessThanOrEqualTo => "meta:lessThanOrEqualTo",
                _ => throw new Exception($"{constraintType} is an invalid comparison operator.")
            };
        }
    }
}
