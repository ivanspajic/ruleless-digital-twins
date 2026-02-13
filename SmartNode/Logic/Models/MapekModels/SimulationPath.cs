namespace Logic.Models.MapekModels
{
    public class SimulationPath
    {
        public required IEnumerable<Simulation> Simulations { get; set; }

        public void RemoveFirstRemainingSimulationFromSimulationPath() {
            var remainingSimulations = Simulations.ToList();
            remainingSimulations.RemoveAt(0);

            Simulations = remainingSimulations;
        }
    }
}
