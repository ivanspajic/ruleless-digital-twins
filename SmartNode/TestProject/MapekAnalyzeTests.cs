using Logic.Mapek;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Reflection;
using TestProject.Mocks;
using TestProject.Mocks.EqualityComparers;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace TestProject
{
    public class MapekAnalyzeTests
    {
        [Fact]
        public void Actions_and_OptimalConditions_for_plan_phase_same_as_expected()
        {
            // Arrange
            var executingAssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var modelFilePath = Path.Combine(executingAssemblyPath!, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}models-and-rules{Path.DirectorySeparatorChar}inferred-model-1.ttl");
            modelFilePath = Path.GetFullPath(modelFilePath);

            var instanceModel = new Graph();
            var turtleParser = new TurtleParser();
            turtleParser.Load(instanceModel, modelFilePath);

            var mapekAnalyze = new MapekAnalyze(new ServiceProviderMock());

            var propertyCacheMock = new PropertyCache
            {
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
                Properties = new Dictionary<string, Property>
                {
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterMeasuredEnergyConsumption",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterMeasuredEnergyConsumption",
                            OwlType = "double",
                            Value = 1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2MeasuredTemperature",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2MeasuredTemperature",
                            OwlType = "double",
                            Value = 1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1MeasuredTemperature",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1MeasuredTemperature",
                            OwlType = "double",
                            Value = 1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AverageRoomTemperature",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AverageRoomTemperature",
                            OwlType = "double",
                            Value = 1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorMeasuredHumidity",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorMeasuredHumidity",
                            OwlType = "double",
                            Value = 1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomHumidity",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomHumidity",
                            OwlType = "double",
                            Value = 1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption",
                            OwlType = "double",
                            Value = 1.02
                        }
                    },
                    {
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature",
                        new Property
                        {
                            Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature",
                            OwlType = "double",
                            Value = 1.02
                        }
                    }
                }
            };
            var expectedOptimalConditions = new List<OptimalCondition>
            {
                new OptimalCondition
                {
                    ConstraintValueType = "double",
                    Constraints = new List<ConstraintExpression>
                    {
                        new AtomicConstraintExpression
                        {
                            ConstraintType = ConstraintType.GreaterThan,
                            Right = "10.1"
                        },
                        new AtomicConstraintExpression
                        {
                            ConstraintType = ConstraintType.LessThanOrEqualTo,
                            Right = "28.5"
                        }
                    },
                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#OptimalCondition2",
                    Property = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature",
                    ReachedInMaximumSeconds = 3600,
                    UnsatisfiedAtomicConstraints = new List<AtomicConstraintExpression>
                    {
                        new AtomicConstraintExpression
                        {
                            ConstraintType = ConstraintType.GreaterThan,
                            Right = "10.1"
                        }
                    }
                },
                new OptimalCondition
                {
                    ConstraintValueType = "double",
                    Constraints = new List<ConstraintExpression>
                    {
                        new AtomicConstraintExpression
                        {
                            ConstraintType = ConstraintType.GreaterThanOrEqualTo,
                            Right = "27.5"
                        },
                        new AtomicConstraintExpression
                        {
                            ConstraintType = ConstraintType.LessThan,
                            Right = "30.2"
                        }
                    },
                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#OptimalCondition1",
                    Property = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature",
                    ReachedInMaximumSeconds = 3600,
                    UnsatisfiedAtomicConstraints = new List<AtomicConstraintExpression>
                    {
                        new AtomicConstraintExpression
                        {
                            ConstraintType = ConstraintType.GreaterThanOrEqualTo,
                            Right = "27.5"
                        }
                    }
                }
            };
            var expectedActions = new List<Logic.Models.OntologicalModels.Action>
            {
                new ActuationAction
                {
                    ActedOnProperty = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature",
                    ActuatorState = new ActuatorState
                    {
                        Actuator = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater",
                        Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HeaterStrong"
                    },
                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HeaterStronghttp://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation/ActuationAction"
                },
                new ActuationAction
                {
                    ActedOnProperty = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature",
                    ActuatorState = new ActuatorState
                    {
                        Actuator = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater",
                        Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HeaterWeak"
                    },
                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HeaterWeakhttp://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation/ActuationAction"
                },
                new ActuationAction
                {
                    ActedOnProperty = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature",
                    ActuatorState = new ActuatorState
                    {
                        Actuator = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater",
                        Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HeaterMedium"
                    },
                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HeaterMediumhttp://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation/ActuationAction"
                },
                new ActuationAction
                {
                    ActedOnProperty = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomHumidity",
                    ActuatorState = new ActuatorState
                    {
                        Actuator = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier",
                        Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#DehumidifierOn"
                    },
                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#DehumidifierOnhttp://www.semanticweb.org/ivans/ontologies/2025/dt-code-generation/ActuationAction"
                },
            };
            var expectedOptimalConditionActionTuple = new Tuple<IEnumerable<OptimalCondition>, IEnumerable<Logic.Models.OntologicalModels.Action>>(expectedOptimalConditions, expectedActions);

            // Act
            var actualOptimalConditionActionTuple = mapekAnalyze.Analyze(instanceModel, propertyCacheMock);

            // Assert
            // Check that the two collections in the tuple have the same number of elements.
            Assert.Equal(expectedOptimalConditionActionTuple.Item1.Count(), actualOptimalConditionActionTuple.Item1.Count());
            Assert.Equal(expectedOptimalConditionActionTuple.Item2.Count(), actualOptimalConditionActionTuple.Item2.Count());
            
            // Check that the OptimalConditions are equal to those expected.
            foreach (var optimalCondition in expectedOptimalConditionActionTuple.Item1)
            {
                Assert.Contains(optimalCondition, actualOptimalConditionActionTuple.Item1, new OptimalConditionEqualityComparer());
            }

            // Check that the Actions are equal to those expected.
            foreach (var action in expectedOptimalConditionActionTuple.Item2)
            {
                Assert.Contains(action, actualOptimalConditionActionTuple.Item2, new ActionEqualityComparer());
            }
        }
    }
}