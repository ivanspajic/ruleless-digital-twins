namespace Logic.Models.MapekModels {
    public class SimulationTreeNode {
        public Simulation Simulation { get; set; }

        public IEnumerable<SimulationTreeNode> Children { get; set; }

        public int ChildrenCount { get; } // TODO

        public IEnumerable<SimulationPath> SimulationPaths { get; } // TODO
    }
}
