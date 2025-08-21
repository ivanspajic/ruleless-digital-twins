using Femyou;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject
{
    public class SimulationTests
    {
        [Fact]
        public void Simulation_loads_and_executes_Fmu()
        {
            // Arrange
            var modelName = "../../../../SensorActuatorImplementations/FMUs/roomM370.fmu";
            var expectedValue = 1.0;
            var actualValue = 0.0;

            // Act
            var model = Model.Load(modelName);
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
    }
}
