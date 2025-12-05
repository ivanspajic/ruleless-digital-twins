namespace Logic.Models.MapekModels {
    public class SimulationTreeNode {
        public Simulation Simulation { get; set; }

        public IEnumerable<SimulationTreeNode> Children { get; set; }

        public int ChildrenCount {
            get {
                var count = 0;

                // Add the count of all children's children including the child itself.
                foreach (var child in Children) {
                    count += child.ChildrenCount + 1;
                }

                return count;
            }
        }

        public IEnumerable<SimulationPath> SimulationPaths {
            get {
                var simulationPaths = new List<SimulationPath>();

                if (ChildrenCount == 0) {
                    if (Simulation.Index != -1) {
                        simulationPaths.Add(new SimulationPath {
                            Simulations = [Simulation]
                        });
                    }

                    return simulationPaths;
                }

                foreach (var child in Children) {
                    var innerSimulationPaths = child.SimulationPaths;

                    foreach (var innerSimulationPath in innerSimulationPaths) {
                        if (Simulation.Index != -1) {
                            var simulationPath = new SimulationPath {
                                Simulations = new List<Simulation> {
                                    Simulation
                                }.Concat(innerSimulationPath.Simulations)
                            };

                            simulationPaths.Add(simulationPath);
                        } else {
                            simulationPaths.Add(innerSimulationPath);
                        }
                    }
                }

                return simulationPaths;
            }
        }
    }
}
