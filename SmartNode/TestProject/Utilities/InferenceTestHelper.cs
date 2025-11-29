using Logic.Models.OntologicalModels;
using System.Reflection;

namespace TestProject.Utilities {
    internal static class InferenceTestHelper {
        public static readonly string SolutionRootDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        public static readonly string TestFileDirectory = Path.Combine(SolutionRootDirectory, "SmartNode", "TestFiles");

        public static TheoryData<string, IEnumerable<IEnumerable<ActuationAction>>> TestData =>
            new() {
                {
                    "unrestrictedDehumidifierFanHeater.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "Fan_0",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Fan_1",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue= "1"
                            },
                            new() {
                                Name = "Heater_0",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Heater_1",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "Heater_2",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "2"
                            }
                        }
                    }
                },
                {
                    "restrictedDehumidifierFanHeater.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "Fan_0",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Fan_1",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue= "1"
                            },
                            new() {
                                Name = "Heater_0",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Heater_1",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "Heater_2",
                                Actuator = new Actuator {
                                    Name = "Heater"
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
                                Name = "Dehumidifier_0",
                                Actuator = new Actuator {
                                    Name = "Dehumidifier"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Dehumidifier_1",
                                Actuator = new Actuator {
                                    Name = "Dehumidifier"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "Fan_0",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Fan_1",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue= "1"
                            },
                            new() {
                                Name = "Heater_0",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Heater_1",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "FloorHeating"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "FloorHeating"
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
                                Name = "Fan_0",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Fan_1",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue= "1"
                            },
                            new() {
                                Name = "Heater_0",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Heater_1",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "FloorHeating_0",
                                Actuator = new Actuator {
                                    Name = "FloorHeating"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "FloorHeating_1",
                                Actuator = new Actuator {
                                    Name = "FloorHeating"
                                },
                                NewStateValue = "1"
                            }
                        }
                    }
                },
                {
                    "restrictedDehumidifierFanHeaterManyStates.ttl",
                    new[] {
                        new ActuationAction[] {
                            new() {
                                Name = "Fan_0",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Fan_1",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue= "1"
                            },
                            new() {
                                Name = "Fan_2",
                                Actuator = new Actuator {
                                    Name = "Fan"
                                },
                                NewStateValue = "2"
                            },
                            new() {
                                Name = "Heater_0",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "0"
                            },
                            new() {
                                Name = "Heater_1",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "1"
                            },
                            new() {
                                Name = "Heater_2",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "2"
                            },
                            new() {
                                Name = "Heater_3",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "3"
                            },
                            new() {
                                Name = "Heater_4",
                                Actuator = new Actuator {
                                    Name = "Heater"
                                },
                                NewStateValue = "4"
                            }
                        }
                    }
                }
            };
    }
}
