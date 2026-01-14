using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Implementations.SimulatedTwinningTargets {

    // Case-sensitive!
    public record class IncubatorFields(double t1, double t2, double t3, double average_temperature, double execution_interval, double elapsed,
                                ulong time_t1, ulong time_t2, ulong time_t3,
                                bool heater_on, bool fan_on);
    public record class Incubator(string measurement, IncubatorFields fields);

    public class IncubatorAdapter {
        private readonly ConnectionFactory _factory;
        private readonly CancellationToken _ct; // Probably shouldn't be "static", but good enough for now.
        public IncubatorFields? Data;
        public ulong Counter = 0;

        private readonly string ExchangeName = "Incubator_AMQP"; // From Incubator
        private readonly double G_box = 0.5763498; // startup.conf

        private IncubatorAdapter _instance;

        public IConnection? Conn { get; private set; }
        public IChannel? Channel { get; private set; }

        public IncubatorAdapter(string hostName, CancellationToken cancellationToken) {

            _factory = new() {
                UserName = "incubator",
                Password = "incubator",
                HostName = hostName
            };
            _ct = cancellationToken;
            _instance = this;
        }

        public async Task Connect() {
            Conn = await _factory.CreateConnectionAsync(cancellationToken: _ct);
            Channel = await Conn.CreateChannelAsync(cancellationToken: _ct);
            await Channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, cancellationToken: _ct);
        }

        public async Task<string> Setup() {
            if (Channel is null) {
                throw new Exception();
            }
            var queueName = "mine_local"; // Under our control
            await Channel.QueueDeclareAsync(queueName, false, false, false, null, cancellationToken: _ct);
            // From Incubator: incubator_state_csv_recorder.py:
            await Channel.QueueBindAsync(queueName, ExchangeName, "incubator.record.driver.state", null, cancellationToken: _ct);

            var _lock = this;

            var consumer = new AsyncEventingBasicConsumer(Channel);
            consumer.ReceivedAsync += async (ch, ea) => {
                var body = ea.Body.ToArray();
                await Channel.BasicAckAsync(ea.DeliveryTag, false);
                string json = Encoding.Default.GetString(body);
                var fromJson = JsonSerializer.Deserialize<Incubator>(json);
                Debug.Assert(fromJson != null);
                // I guess the fun starts when async events start overtaking each other here.
                // TODO: Use a proper queue.
                Monitor.Enter(_lock);
                Data = fromJson.fields;
                Counter++;
                Monitor.Exit(_lock);
            };
            return await Channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken: _ct);
        }

        record class GBox(double G_box);

        // software/cli/mess_with_lid_mock.py
        public async Task SendGBoxConfig(double g) {
            if (Channel == null) {
                throw new Exception();
            }
            var new_g_box = new GBox(g * G_box);
            // byte[] messageBodyBytes = Encoding.UTF8.GetBytes($"{{\"G_box\": {new_g_box}}}"); // Arrrrr
            byte[] messageBodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize<GBox>(new_g_box)); // Arrrrr
            var props = new BasicProperties();
            await Channel.BasicPublishAsync(ExchangeName, "incubator.mock.hw.box.G", false, props, messageBodyBytes);
        }

        record class Heater(Boolean heater);
        public async Task SetHeater(Boolean on) {
            if (Channel == null) {
                throw new Exception();
            }
            var new_heater = new Heater(on);
            byte[] messageBodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize<Heater>(new_heater)); // Arrrrr
            var props = new BasicProperties();
            await Channel.BasicPublishAsync(ExchangeName, "incubator.hardware.gpio.heater.on", false, props, messageBodyBytes);
        }
    }
}