using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TestProject {

public class IncubatorAMQPTest
    {
        [Theory]
        [InlineData("192.168.64.1")] // Should probably come from the outside since it depends where your Incubator-container is running.
        public async Task TestConnect(string hostName) {
            ConnectionFactory factory = new() {
                UserName = "incubator",
                Password = "incubator",
                HostName = hostName
            };

            IConnection conn = await factory.CreateConnectionAsync(cancellationToken: TestContext.Current.CancellationToken);
            IChannel channel = await conn.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
            var exchangeName = "Incubator_AMQP"; // From Incubator
            var queueName = "mine_local"; // Under our control
            var routingKey = "incubator.record.driver.state"; // From Incubator: incubator_state_csv_recorder.py
            await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, cancellationToken: TestContext.Current.CancellationToken);
            await channel.QueueDeclareAsync(queueName, false, false, false, null, cancellationToken: TestContext.Current.CancellationToken);
            await channel.QueueBindAsync(queueName, exchangeName, routingKey, null, cancellationToken: TestContext.Current.CancellationToken);

            var tcs = new TaskCompletionSource<object>();
            var consumer = new AsyncEventingBasicConsumer(channel);
            var counter = 3;
            consumer.ReceivedAsync += async (ch, ea) => {
                                var body = ea.Body.ToArray();
                                await channel.BasicAckAsync(ea.DeliveryTag, false);
                                // For the Incubator it's JSON:
                                // Nope out after three messages:
                                if (counter-- <= 0) {                                    
                                    tcs.SetResult(null);
                                } else { // eat reamining messages so that they don't spam the console.
                                    System.Diagnostics.Trace.WriteLine(Encoding.Default.GetString(body));
                                }
                            };
            string consumerTag = await channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken: TestContext.Current.CancellationToken);
            System.Diagnostics.Trace.WriteLine("Waiting. Tag: " + consumerTag);
            await tcs.Task;
            System.Diagnostics.Trace.WriteLine("Done.");
        }
    }
}