using Logic.Models.OntologicalModels;
using System.Reflection;

namespace TestProject.Utilities {
    internal static class InferenceTestHelper {
        public static readonly string SolutionRootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        public static readonly string TestFileDirectory = Path.Combine(SolutionRootDirectory, "SmartNode", "TestProject", "TestFiles");

        public static TheoryData<string, IEnumerable<IEnumerable<ActuationAction>>> TestData =>
            new() {
                {
                    "unrestrictedDehumidifierFanHeater.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            }
                        }
                    }
                },
                /*{
                    "restrictedDehumidifierFanHeater.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            }
                        }
                    }
                },
                {
                    "unrestrictedDehumidifierFanHeaterFloorHeating.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Fan"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        }
                    }
                },
                {
                    "restrictedDehumidifierFanHeaterFloorHeating.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        }
                    }
                },
                {
                    "restrictedDehumidifierHeaterFloorHeatingManyStates.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "2"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "2"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "2"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "2"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_3",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "3"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_3",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "3"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_3",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "3"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "2"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_4",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "4"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "0"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_4",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "4"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        },
                        new ActuationAction[] {
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater_4",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater"
                                },
                                NewStateValue = "4"
                            },
                            new() {
                                Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating_2",
                                Actuator = new Actuator {
                                    Name = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating"
                                },
                                NewStateValue = "2"
                            }
                        }
                    }
                }*/
            };
    }
}
