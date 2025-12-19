using Logic.TTComponentInterfaces;

namespace Implementations.SoftwareComponents {
    public class DummyConfigurableParameter : IConfigurableParameter {
        public DummyConfigurableParameter(string name) {
            Name = name;
        }

        public string Name { get; private set; }

        public void UpdateConfigurableParameter(string configurableParameterName, object configurableParameterValue) {
            // TODO: implement a connection with the real MixPiece/CustomPiece algorithm.
        }
    }
}
