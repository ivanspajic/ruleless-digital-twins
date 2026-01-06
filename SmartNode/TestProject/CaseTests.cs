using CsvHelper;
using Implementations.ValueHandlers;
using Logic.CaseRepository;
using Logic.FactoryInterface;
using Logic.Mapek;
using Logic.Mapek.Comparers;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using TestProject.Mocks;

namespace TestProject {
    public class CaseTests {
        [Fact]
        public void Saved_case_matched() {
            // Arrange


            // Act


            // Assert

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
                        Property = "FakeProperty1",
                        ConstraintValueType = "http://www.w3.org/2001/XMLSchema#double",
                        UnsatisfiedAtomicConstraints = [],
                        Constraints = new List<ConstraintExpression> {
                            new AtomicConstraintExpression {
                                ConstraintType = ConstraintType.GreaterThan,
                                Right = 22.456
                            }
                        }
                    },
                    new OptimalCondition {
                        Name = "FakeOptimalCondition2",
                        ReachedInMaximumSeconds = 0,
                        Property = "FakeProperty2",
                        ConstraintValueType = "http://www.w3.org/2001/XMLSchema#double",
                        UnsatisfiedAtomicConstraints = [],
                        Constraints = new List<ConstraintExpression> {
                            new AtomicConstraintExpression {
                                ConstraintType = ConstraintType.LessThanOrEqualTo,
                                Right = 12.345
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
            var coordinatorSettings = new CoordinatorSettings {
                LookAheadMapekCycles = 2,
                MaximumMapekRounds = 2,
                PropertyValueFuzziness = 0.25,
                SaveMapekCycleData = false,
                SimulationDurationSeconds = 100,
                StartInReactiveMode = false,
                UseSimulatedEnvironment = true
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
                        Property = "FakeProperty1",
                        ConstraintValueType = "http://www.w3.org/2001/XMLSchema#double",
                        UnsatisfiedAtomicConstraints = [],
                        Constraints = new List<ConstraintExpression> {
                            new AtomicConstraintExpression {
                                ConstraintType = ConstraintType.GreaterThan,
                                Right = 22.5
                            }
                        }
                    },
                    new OptimalCondition {
                        Name = "FakeOptimalCondition2",
                        ReachedInMaximumSeconds = 0,
                        Property = "FakeProperty2",
                        ConstraintValueType = "http://www.w3.org/2001/XMLSchema#double",
                        UnsatisfiedAtomicConstraints = [],
                        Constraints = new List<ConstraintExpression> {
                            new AtomicConstraintExpression {
                                ConstraintType = ConstraintType.LessThanOrEqualTo,
                                Right = 12.25
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
            mapekManager.StartLoop();

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
