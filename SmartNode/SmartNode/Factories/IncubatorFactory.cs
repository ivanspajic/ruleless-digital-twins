using Implementations.Actuators.Incubator;
using Implementations.Sensors.Incubator;
using Implementations.SimulatedTwinningTargets;
using Logic.FactoryInterface;
using Logic.TTComponentInterfaces;

namespace SmartNode.Factories {
    public class IncubatorFactory : AbstractFactory, IFactory {
        // Changing the environment variable's value requires restarting Visual Studio before it's visible.
        private const string HostNameEnvironmentVariableName = "AU_INCUBATOR_RABBITMQ_HOST_NAME";

        private IncubatorAdapter? _incubatorAdapter;

        public IncubatorFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }

        protected override IDictionary<string, IActuator> MakeActuatorMap(IServiceProvider serviceProvider) {
            EnsureIncubatorInstance();

            return new Dictionary<string, IActuator> {
                {
                    "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#HeaterActuator",
                    new AmqHeater(_incubatorAdapter)
                },
                {
                    "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#FanActuator",
                    new AmqFan(_incubatorAdapter)
                }
            };
        }

        protected override IDictionary<string, IConfigurableParameter> MakeConfigurableParameterMap(IServiceProvider serviceProvider) {
            throw new NotImplementedException();
        }

        protected override IDictionary<(string, string), ISensor> MakeSensorMap(IServiceProvider serviceProvider) {
            EnsureIncubatorInstance();

            return new Dictionary<(string, string), ISensor> {
                {
                    ("http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor",
                    "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure"),
                    new AmqSensor(_incubatorAdapter, "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor",
                        "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure",
                        d => d.average_temperature)
                }
            };
        }

        private void EnsureIncubatorInstance() {
            // TODO: Might as well directly come from its own section in the ConfigurationSettings.
            var hostName = Environment.GetEnvironmentVariable(HostNameEnvironmentVariableName) ?? "localhost";
            _incubatorAdapter = new IncubatorAdapter(hostName, new CancellationToken());
            Task t = Task.Run(async () => {
                await _incubatorAdapter.Connect();
                await _incubatorAdapter.Setup();
            });
            t.Wait();
        }
    }
}
