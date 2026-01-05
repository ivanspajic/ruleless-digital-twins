using Implementations.ValueHandlers;
using Logic.Mapek;
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
            // Make a FilepathArguments POCO for MapekManager, but don't include any real paths as these won't be necessary.
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

            var filepathArguments = new FilepathArguments {
                DataDirectory = "",
                FmuDirectory = "",
                InferenceEngineFilepath = "",
                InferenceRulesFilepath = "",
                InferredModelFilepath = "",
                InstanceModelFilepath = "",
                OntologyFilepath = ""
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
            var databaseSettings = new DatabaseSettings {
                CollectionName = "",
                ConnectionString = "",
                DatabaseName = ""
            };
            var serviceProviderMock = new ServiceProviderMock();
            serviceProviderMock.Add(filepathArguments);
            serviceProviderMock.Add(databaseSettings);
            serviceProviderMock.Add(coordinatorSettings);

            var doubleValueHandler = new DoubleValueHandler();
            var factoryMock = new FactoryMock();
            factoryMock.AddValueHandlerImplementation("http://www.w3.org/2001/XMLSchema#double", doubleValueHandler);
            serviceProviderMock.Add(factoryMock);

            var caseRepositoryMock = new CaseRepositoryMock();
            var mapekMonitorMock = new MapekMonitorMock(observedCacheMock);
            var mapekPlanMock = new MapekPlanMock((simulationTreeNode, simulationPath));
            var mapekExecuteMock = new MapekExecuteMock();
            serviceProviderMock.Add(caseRepositoryMock);
            serviceProviderMock.Add(mapekMonitorMock);
            serviceProviderMock.Add(mapekPlanMock);
            serviceProviderMock.Add(mapekExecuteMock);
            var mapekManager = new MapekManager(serviceProviderMock);

            // Act
            mapekManager.StartLoop();

            // Assert
            // TODO: make sure the case saved in the mock is the same as expected
        }
    }
}
