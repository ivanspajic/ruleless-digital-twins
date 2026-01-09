using System.Diagnostics;
using Implementations.SimulatedTwinningTargets;

namespace TestProject {

    // NB: First time you might see a single wild {c} value in the 20s. Rerunning then gives you around 5.
    public class IncubatorAMQPTest {
        [Theory(Explicit = true)]
        [InlineData("192.168.64.1")] // Should probably come from the outside since it depends where your Incubator-container is running.
        public async Task TestConnect(string hostName) {
            var i = IncubatorAdapter.GetInstance(hostName, TestContext.Current.CancellationToken);
            await i.Connect();
            var consumerTag = await i.Setup();
            IncubatorFields? myData = null;
            bool seenFirstData = false; // Safety net. Should be monotone, and myData never turn null again (unless JSON fails).
            ulong c = 0;
            while (c < 25) {
                // Incubator by default ticks every 3 seconds, so we may see some repetitions here:
                Thread.Sleep(TimeSpan.FromSeconds(2));
                Monitor.Enter(i);
                c = i.Counter;
                myData = i.Data;
                Monitor.Exit(i);
                if (myData != null || seenFirstData) {
                    Assert.NotNull(myData);
                    // TODO: format human readable time.
                    Trace.WriteLine($"Observed ({c}): {myData.average_temperature} @ {myData.time_t1}");
                    seenFirstData = true;
                }
                if (c == 10) {
                    Trace.WriteLine("Opening the lid...");
                    await i.SendGBoxConfig(100);
                }
                if (c == 8) {
                    Trace.WriteLine("Micromanaging heater...");
                    await i.SetHeater(!myData.heater_on);
                }
                if (c == 20) {
                    Trace.WriteLine("Closing the lid...");
                    await i.SendGBoxConfig(1);
                }
            }
        }
    }
}