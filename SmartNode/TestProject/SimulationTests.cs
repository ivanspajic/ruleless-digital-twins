using Femyou;
using System.Reflection;

namespace TestProject
{
    public class SimulationTests
    {
        [Fact]
        public void Simulation_loads_and_executes_Fmu()
        {
            // Arrange
            // Use a dummy-type to get a handle:
            var assembly = Assembly.GetAssembly(typeof(Implementations.ValueHandlers.DoubleValueHandler));            
            Stream modelStream = assembly.GetManifestResourceStream("Implementations.FMUs.roomM370.fmu");
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

        [Theory]
        [InlineData(900)]
        public void Fmu_returns_same_value_after_specific_simulation_time(int simulationTimeSeconds)
        {
            // Arrange
            // Use a dummy-type to get a handle:
            var assembly = Assembly.GetAssembly(typeof(Implementations.ValueHandlers.DoubleValueHandler));
            Stream modelStream = assembly.GetManifestResourceStream("Implementations.FMUs.roomM370.fmu");
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
