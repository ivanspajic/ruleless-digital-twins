using Femyou;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TestProject
{
    public class SimulationTests
    {
        private readonly string _archStr;
        private readonly Assembly? _assembly;

        public SimulationTests()
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            _archStr = arch == Architecture.Arm64 ? "arm64" : (arch == Architecture.X64 ? "amd64" : throw new NotSupportedException());

            // Use a dummy-type to get a handle:
            _assembly = Assembly.GetAssembly(typeof(Implementations.ValueHandlers.DoubleValueHandler));
        }

        [Fact]
        public void Simulation_loads_and_executes_Fmu()
        {
            // Arrange
            Stream modelStream = _assembly.GetManifestResourceStream("Implementations.FMUs.roomM370.fmu");
            Assert.NotNull(modelStream);
            var expectedValue = 1.0;
            var actualValue = 0.0;

            // Act
            var model = Model.Load(modelStream, "roomM370.fmu");
            var energyConsumption = model.Variables["EnergyConsumption"];

            var fmuInstance = model.CreateCoSimulationInstance("demo");

            fmuInstance.StartTime(0);
            fmuInstance.WriteReal((energyConsumption, expectedValue));
            fmuInstance.AdvanceTime(10);
            actualValue = fmuInstance.ReadReal(energyConsumption).ToArray()[0];

            fmuInstance.Dispose();
            model.Dispose();

            // Assert
            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public void Simulation_loads_and_executes_Nordpool_Fmu()
        {
            // Arrange
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                string? s = Environment.GetEnvironmentVariable("LD_PRELOAD");
                Assert.Equal($"/usr/lib/{_archStr}-linux-gnu/libpython3.11.so", s); // You're not running in Debug-mode!
            }
            // Use a dummy-type to get a handle:
            Stream modelStream = _assembly.GetManifestResourceStream("Implementations.FMUs.Nordpool_FMU.NordPool.fmu");
            Assert.NotNull(modelStream);
            var expectedValue = -100.0;

            // Act
            var model = Model.Load(modelStream, "NordPool.fmu");
            var price = model.Variables["price"];

            var fmuInstance = model.CreateCoSimulationInstance("demo");

            fmuInstance.StartTime(0);
            fmuInstance.AdvanceTime(10);
            var actualValue = fmuInstance.ReadReal(price).ToArray()[0];
            var notFound = fmuInstance.ReadBoolean(model.Variables["notFound"]).ToArray()[0];
            Assert.False(notFound);
            fmuInstance.Dispose();
            model.Dispose();

            // Assert
            Assert.NotEqual(expectedValue, actualValue);
        }

        [Fact]
        public void Simulation_loads_and_executes_Nordpool_Fmu_TooLate()
        {
            // Arrange
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                string? s = Environment.GetEnvironmentVariable("LD_PRELOAD");
                Assert.Equal($"/usr/lib/{_archStr}-linux-gnu/libpython3.11.so", s); // You're not running in Debug-mode!
            }
            Stream modelStream = _assembly.GetManifestResourceStream("Implementations.FMUs.Nordpool_FMU.NordPool.fmu");
            Assert.NotNull(modelStream);

            // Act
            var model = Model.Load(modelStream, "NordPool.fmu");
            var price = model.Variables["price"];

            var fmuInstance = model.CreateCoSimulationInstance("demo");

            fmuInstance.StartTime(36*60*60);
            var notFound = fmuInstance.ReadBoolean(model.Variables["notFound"]).ToArray()[0];
            Assert.True(notFound);
            fmuInstance.Dispose();
            model.Dispose();
        }


        [Theory]
        [InlineData(900)]
        public void Fmu_returns_same_value_after_specific_simulation_time(int simulationTimeSeconds)
        {
            // Arrange
            Stream modelStream = _assembly.GetManifestResourceStream("Implementations.FMUs.roomM370.fmu");
            Assert.NotNull(modelStream);

            var roomTemperatureInitialValue = 1.02;
            var acUnitStateValue = 1;
            var expectedValue = 11.0965;
            var actualValue = 0.0;

            // Act
            var model = Model.Load(modelStream, "roomM370.fmu");
            var fmuInstance = model.CreateCoSimulationInstance("demo");

            var roomTemperature = model.Variables["RoomTemperature"];
            var acUnitState = model.Variables["AirConditioningUnitState"];

            fmuInstance.StartTime(0);

            fmuInstance.WriteReal((roomTemperature, roomTemperatureInitialValue));
            fmuInstance.WriteInteger((acUnitState, acUnitStateValue));

            fmuInstance.AdvanceTime(simulationTimeSeconds);

            actualValue = fmuInstance.ReadReal(roomTemperature).ToArray()[0];

            fmuInstance.Dispose();
            model.Dispose();

            // Assert
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
