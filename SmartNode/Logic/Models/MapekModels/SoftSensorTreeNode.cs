using Logic.TTComponentInterfaces;

namespace Logic.Models.MapekModels {
    public class SoftSensorTreeNode : ITreeNode<ISensorDevice, SoftSensorTreeNode> {
        public ISensorDevice NodeItem { get; set; }

        public IEnumerable<SoftSensorTreeNode> Children { get; set; }

        public string OutputProperty { get; set; }
    }
}
