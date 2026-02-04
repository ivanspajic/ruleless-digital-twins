using Implementations.ValueHandlers;
using Logic.CaseRepository;
using Logic.Mapek;
using Logic.Mapek.Comparers;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using MongoDB.Driver;
using TestProject.Mocks.MongoDB;
using TestProject.Mocks.ServiceMocks;

namespace TestProject {
    public class CaseTests {
        [Fact]
        public void Case_repository_logic_finds_right_case() {
            // Arrange
            // Set up all the DTOs.
            var databaseSettings = new DatabaseSettings {
                CollectionName = "Cases",
                ConnectionString = "mongodb://localhost:27017",
                DatabaseName = "CaseBase"
            };
            var simulationResultCache = new PropertyCache {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter> { },
                Properties = new Dictionary<string, Property> {
                    {
                        "FakeProperty1",
                        new Property {
                            Name = "FakeProperty1",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 22.689
                        }
                    },
                    {
                        "FakeProperty2",
                        new Property {
                            Name = "FakeProperty2",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 11.988
                        }
                    }
                }
            };
            var simulationPath = new SimulationPath {
                Simulations = new List<Simulation> {
                    new Simulation(simulationResultCache) {
                        Index = 0,
                        InitializationActions = [],
                        Actions = new List<Logic.Models.OntologicalModels.Action> {
                            new ActuationAction {
                                Name = "FakeActuationAction1",
                                Actuator = new Actuator {
                                    Name = "FakeActuator1"
                                },
                                NewStateValue = 1
                            },
                            new ActuationAction {
                                Name = "FakeActuationAction2",
                                Actuator = new Actuator {
                                    Name = "FakeActuator2"
                                },
                                NewStateValue = 3
                            },
                        }
                    },
                    new Simulation(simulationResultCache) {
                        Index = 1,
                        InitializationActions = [],
                        Actions = new List<Logic.Models.OntologicalModels.Action> {
                            new ActuationAction {
                                Name = "FakeActuationAction3",
                                Actuator = new Actuator {
                                    Name = "FakeActuator1"
                                },
                                NewStateValue = 2
                            },
                            new ActuationAction {
                                Name = "FakeActuationAction4",
                                Actuator = new Actuator {
                                    Name = "FakeActuator2"
                                },
                                NewStateValue = 4
                            },
                        }
                    }
                }
            };
            var quantizedProperties = new List<Property> {
                new Property {
                    Name = "FakeProperty1",
                    OwlType = "http://www.w3.org/2001/XMLSchema#double",
                    Value = 22.75
                },
                new Property {
                    Name = "FakeProperty2",
                    OwlType = "http://www.w3.org/2001/XMLSchema#double",
                    Value = 12
                }
            };
            var quantizedOptimalConditions = new List<OptimalCondition> {
                new OptimalCondition {
                    Name = "FakeOptimalCondition1",
                    ReachedInMaximumSeconds = 0,
                    Property = quantizedProperties[0],
                    Constraint = new AtomicConstraintExpression {
                        ConstraintType = ConstraintType.GreaterThan,
                        Property = new Property {
                            Name = "OptimalConditionProperty1",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 22.5
                        }
                    }
                },
                new OptimalCondition {
                    Name = "FakeOptimalCondition2",
                    ReachedInMaximumSeconds = 0,
                    Property = quantizedProperties[1],
                    Constraint = new AtomicConstraintExpression {
                        ConstraintType = ConstraintType.LessThanOrEqualTo,
                        Property = new Property {
                            Name = "OptimalConditionProperty2",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 12.25
                        }
                    }
                }
            };
            var caseIndex = 0;
            var lookAheadCycles = 2;
            var simulationDurationSeconds = 100;
            var expectedCase = new Case {
                ID = null,
                Index = 0,
                LookAheadCycles = 2,
                SimulationDurationSeconds = 100,
                Simulation = simulationPath.Simulations.First(),
                QuantizedOptimalConditions = new List<OptimalCondition> {
                    new OptimalCondition {
                        Name = "FakeOptimalCondition1",
                        ReachedInMaximumSeconds = 0,
                        Property = quantizedProperties[0],
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.GreaterThan,
                            Property = new Property {
                                Name = "OptimalConditionProperty1",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 22.5
                            }
                        }
                    },
                    new OptimalCondition {
                        Name = "FakeOptimalCondition2",
                        ReachedInMaximumSeconds = 0,
                        Property = quantizedProperties[1],
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.LessThanOrEqualTo,
                            Property = new Property {
                                Name = "OptimalConditionProperty2",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 12.25
                            }
                        }
                    }
                },
                QuantizedProperties = new List<Property> {
                    new Property {
                        Name = "FakeProperty1",
                        OwlType = "http://www.w3.org/2001/XMLSchema#double",
                        Value = 22.75
                    },
                    new Property {
                        Name = "FakeProperty2",
                        OwlType = "http://www.w3.org/2001/XMLSchema#double",
                        Value = 12
                    }
                }
            };
            var otherCase = new Case {
                ID = null,
                Index = 0,
                LookAheadCycles = 2,
                SimulationDurationSeconds = 150,
                Simulation = simulationPath.Simulations.First(),
                QuantizedOptimalConditions = new List<OptimalCondition> {
                    new OptimalCondition {
                        Name = "FakeOptimalCondition1",
                        ReachedInMaximumSeconds = 0,
                        Property = quantizedProperties[1],
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.GreaterThan,
                            Property = new Property {
                                Name = "OptimalConditionProperty1",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 22.75
                            }
                        }
                    },
                    new OptimalCondition {
                        Name = "FakeOptimalCondition2",
                        ReachedInMaximumSeconds = 0,
                        Property = quantizedProperties[1],
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.LessThanOrEqualTo,
                            Property = new Property {
                                Name = "OptimalConditionProperty2",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 12.25
                            }
                        }
                    }
                },
                QuantizedProperties = new List<Property> {
                    new Property {
                        Name = "FakeProperty3",
                        OwlType = "http://www.w3.org/2001/XMLSchema#double",
                        Value = 62.75
                    },
                    new Property {
                        Name = "FakeProperty2",
                        OwlType = "http://www.w3.org/2001/XMLSchema#double",
                        Value = 16.25
                    }
                }
            };

            // Set up all the services and mocks.
            var serviceProviderMock = new ServiceProviderMock();
            var mongoDatabaseMock = new MongoDatabaseMock();
            var caseCollectionMock = new MongoCollectionMock<Case>();
            mongoDatabaseMock.AddCollection(caseCollectionMock);
            var mongoClientMock = new MongoClientMock(mongoDatabaseMock);
            serviceProviderMock.Add(databaseSettings);
            serviceProviderMock.Add<IMongoClient>(mongoClientMock);
            var caseRepository = new CaseRepository(serviceProviderMock);

            // Act
            caseRepository.CreateCase(expectedCase);
            caseRepository.CreateCase(otherCase);
            var actualCase = caseRepository.ReadCase(quantizedProperties, quantizedOptimalConditions, lookAheadCycles, simulationDurationSeconds, caseIndex);

            // Assert
            Assert.Equal(expectedCase, actualCase);
        }

        [Fact]
        public void Successful_case_saved() {
            // Arrange
            // Set up all the DTOs.

            var observedCacheMock = new Cache {
                OptimalConditions = new List<OptimalCondition> {
                    new OptimalCondition {
                        Name = "FakeOptimalCondition1",
                        ReachedInMaximumSeconds = 0,
                        Property = new Property {
                            Name = "FakeProperty1",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 22.674
                        },
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.GreaterThan,
                            Property = new Property {
                                Name = "OptimalConditionProperty1",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 22.456
                            }
                        }
                    },
                    new OptimalCondition {
                        Name = "FakeOptimalCondition2",
                        ReachedInMaximumSeconds = 0,
                        Property = new Property {
                            Name = "FakeProperty2",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 11.995
                        },
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.LessThanOrEqualTo,
                            Property = new Property {
                                Name = "OptimalConditionProperty2",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 12.345
                            }
                        }
                    }
                },
                SoftSensorTreeNodes = new List<SoftSensorTreeNode>(),
                PropertyCache = new PropertyCache {
                    ConfigurableParameters = new Dictionary<string, ConfigurableParameter> { },
                    Properties = new Dictionary<string, Property> {
                        {
                            "FakeProperty1",
                            new Property {
                                Name = "FakeProperty1",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 22.674
                            }
                        },
                        {
                            "FakeProperty2",
                            new Property {
                                Name = "FakeProperty2",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 11.995
                            }
                        }
                    }
                }
            };
            var simulationResultCache = new PropertyCache {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter> { },
                Properties = new Dictionary<string, Property> {
                    {
                        "FakeProperty1",
                        new Property {
                            Name = "FakeProperty1",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 22.689
                        }
                    },
                    {
                        "FakeProperty2",
                        new Property {
                            Name = "FakeProperty2",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 11.988
                        }
                    }
                }
            };
            var simulationTreeNode = new SimulationTreeNode();
            var simulationPath = new SimulationPath {
                Simulations = new List<Simulation> {
                    new Simulation(simulationResultCache) {
                        Index = 0,
                        InitializationActions = [],
                        Actions = new List<Logic.Models.OntologicalModels.Action> {
                            new ActuationAction {
                                Name = "FakeActuationAction1",
                                Actuator = new Actuator {
                                    Name = "FakeActuator1"
                                },
                                NewStateValue = 1
                            },
                            new ActuationAction {
                                Name = "FakeActuationAction2",
                                Actuator = new Actuator {
                                    Name = "FakeActuator2"
                                },
                                NewStateValue = 3
                            },
                        }
                    },
                    new Simulation(simulationResultCache) {
                        Index = 1,
                        InitializationActions = [],
                        Actions = new List<Logic.Models.OntologicalModels.Action> {
                            new ActuationAction {
                                Name = "FakeActuationAction3",
                                Actuator = new Actuator {
                                    Name = "FakeActuator1"
                                },
                                NewStateValue = 2
                            },
                            new ActuationAction {
                                Name = "FakeActuationAction4",
                                Actuator = new Actuator {
                                    Name = "FakeActuator2"
                                },
                                NewStateValue = 4
                            },
                        }
                    }
                }
            };

            // Make FilepathArguments and DatabaseSettings POCOs for MapekManager, but don't include any real content as it won't be necessary.
            var filepathArguments = new FilepathArguments {
                DataDirectory = "",
                FmuDirectory = "",
                InferenceEngineFilepath = "",
                InferenceRulesFilepath = "",
                InferredModelFilepath = "",
                InstanceModelFilepath = "",
                OntologyFilepath = ""
            };
            var databaseSettings = new DatabaseSettings {
                CollectionName = "",
                ConnectionString = "",
                DatabaseName = ""
            };
            // Set up the coordinator to only perform 2 MAPE-K cycles, which should be enough for this test.
            var coordinatorSettings = new CoordinatorSettings {
                Environment = "roomM370",
                SaveMapekCycleData = false,
                StartInReactiveMode = false,
                UseCaseBasedFunctionality = true,
                LookAheadMapekCycles = 2,
                MaximumMapekRounds = 2,
                PropertyValueFuzziness = 0.25,
                SimulationDurationSeconds = 100
            };
            var serviceProviderMock = new ServiceProviderMock();
            serviceProviderMock.Add(filepathArguments);
            serviceProviderMock.Add(databaseSettings);
            serviceProviderMock.Add(coordinatorSettings);

            // Set up all the services.
            var doubleValueHandler = new DoubleValueHandler();
            var factoryMock = new FactoryMock();
            factoryMock.AddValueHandlerImplementation("http://www.w3.org/2001/XMLSchema#double", doubleValueHandler);
            serviceProviderMock.Add<Logic.FactoryInterface.IFactory>(factoryMock);

            var caseRepositoryMock = new CaseRepositoryMock();
            var mapekKnowledgeMock = new MapekKnowledgeMock();
            var mapekMonitorMock = new MapekMonitorMock(observedCacheMock);
            var mapekPlanMock = new MapekPlanMock((simulationTreeNode, simulationPath));
            var mapekExecuteMock = new MapekExecuteMock();
            serviceProviderMock.Add<ICaseRepository>(caseRepositoryMock);
            serviceProviderMock.Add<IMapekKnowledge>(mapekKnowledgeMock);
            serviceProviderMock.Add<IMapekMonitor>(mapekMonitorMock);
            serviceProviderMock.Add<IMapekPlan>(mapekPlanMock);
            serviceProviderMock.Add<IMapekExecute>(mapekExecuteMock);
            var mapekManager = new MapekManager(serviceProviderMock);

            // Make the expected case containing the same properties as in the observed cache but with quantized values.
            var expectedCase = new Case {
                ID = null,
                Index = 0,
                LookAheadCycles = 2,
                SimulationDurationSeconds = 100,
                Simulation = simulationPath.Simulations.First(),
                QuantizedOptimalConditions = new List<OptimalCondition> {
                    new OptimalCondition {
                        Name = "FakeOptimalCondition1",
                        ReachedInMaximumSeconds = 0,
                        Property = new Property {
                            Name = "FakeProperty1",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 22.674
                        },
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.GreaterThan,
                            Property = new Property {
                                Name = "OptimalConditionProperty1",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 22.5
                            }
                        }
                    },
                    new OptimalCondition {
                        Name = "FakeOptimalCondition2",
                        ReachedInMaximumSeconds = 0,
                        Property = new Property {
                            Name = "FakeProperty2",
                            OwlType = "http://www.w3.org/2001/XMLSchema#double",
                            Value = 11.995
                        },
                        Constraint = new AtomicConstraintExpression {
                            ConstraintType = ConstraintType.LessThanOrEqualTo,
                            Property = new Property {
                                Name = "OptimalConditionProperty2",
                                OwlType = "http://www.w3.org/2001/XMLSchema#double",
                                Value = 12.25
                            }
                        }
                    }
                },
                QuantizedProperties = new List<Property> {
                    new Property {
                        Name = "FakeProperty1",
                        OwlType = "http://www.w3.org/2001/XMLSchema#double",
                        Value = 22.75
                    },
                    new Property {
                        Name = "FakeProperty2",
                        OwlType = "http://www.w3.org/2001/XMLSchema#double",
                        Value = 12
                    }
                }
            };

            // Act
            mapekManager.StartLoop().Wait();

            var actualCase = caseRepositoryMock.Cases[0];

            // Assert
            Assert.Equal(expectedCase.Index, actualCase.Index);
            Assert.Equal(expectedCase.LookAheadCycles, actualCase.LookAheadCycles);
            Assert.Equal(expectedCase.Simulation.Index, actualCase.Simulation!.Index);
            Assert.Equal(expectedCase.Simulation.Actions, actualCase.Simulation.Actions, new ActionEqualityComparer());
            Assert.Equal(expectedCase.QuantizedProperties, actualCase.QuantizedProperties, new PropertyEqualityComparer());
            Assert.Equal(expectedCase.QuantizedOptimalConditions, actualCase.QuantizedOptimalConditions, new OptimalConditionEqualityComparer());
        }
    }
}
